using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NidusVision.Web.Auth;

namespace NidusVision.Tests;

public sealed class AuthEndpointTests
{
    [Fact]
    public async Task ParallelSetupCreatesOneAdmin()
    {
        using var app = new AuthApp();
        using var first = app.CreateClient();
        using var second = app.CreateClient();

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/auth/setup", new { password = "first-password" }),
            second.PostAsJsonAsync("/api/auth/setup", new { password = "second-password" }));

        responses.Select(r => r.StatusCode).OrderBy(code => code).Must().BeSequenceEqual(
            [HttpStatusCode.OK, HttpStatusCode.Conflict]);
    }

    [Fact]
    public async Task RepeatedFailedLoginsAreThrottled()
    {
        using var app = new AuthApp();
        using var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/setup", new { password = "correct-horse" }))
            .StatusCode.Must().Be(HttpStatusCode.OK);

        for (var attempt = 0; attempt < LoginThrottle.MaxFailures; attempt++)
        {
            (await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong-password" }))
                .StatusCode.Must().Be(HttpStatusCode.Unauthorized);
        }

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { password = "correct-horse" });

        locked.StatusCode.Must().Be(HttpStatusCode.TooManyRequests);
        (locked.Headers.RetryAfter is not null).Must().BeTrue();
    }

    [Fact]
    public async Task SuccessfulLoginClearsFailures()
    {
        using var app = new AuthApp();
        using var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/auth/setup", new { password = "correct-horse" });

        for (var round = 0; round < 2; round++)
        {
            for (var attempt = 0; attempt < LoginThrottle.MaxFailures - 1; attempt++)
            {
                await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong-password" });
            }

            (await client.PostAsJsonAsync("/api/auth/login", new { password = "correct-horse" }))
                .StatusCode.Must().Be(HttpStatusCode.OK);
        }
    }

    private sealed class AuthApp : IDisposable
    {
        private readonly TestDirectory _root = new("nidus-auth");
        private readonly WebApplicationFactory<Program> _factory;

        public AuthApp()
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(_root.Path);
                builder.UseSetting("Storage:DataDirectory", _root.GetPath("data"));
                builder.UseSetting("Storage:RecordingsDirectory", _root.GetPath("recordings"));
            });
        }

        public HttpClient CreateClient() =>
            _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        public void Dispose()
        {
            _factory.Dispose();
            try
            {
                _root.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
