using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NidusVision.Tests;

public sealed class SpaStaticFileAuthTests : IClassFixture<SpaStaticFileAuthTests.App>
{
    private readonly HttpClient _client;

    public SpaStaticFileAuthTests(App app) => _client = app.Client;

    [Fact]
    public async Task AnonymousFaviconIsServed()
    {
        var response = await _client.GetAsync("/favicon.ico");

        response.StatusCode.Must().Be(HttpStatusCode.OK);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        mediaType.Must().Be("image/x-icon");
    }

    [Fact]
    public async Task AnonymousRootReturnsShell()
    {
        var response = await _client.GetAsync("/");

        response.StatusCode.Must().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Must().Contain("Nidus shell");
    }

    [Fact]
    public async Task AnonymousHealthDoesNotExposeTheFfmpegPath()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Must().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Must().Contain("ffmpegAvailable");
        body.Contains("ffmpegPath", StringComparison.OrdinalIgnoreCase).Must().BeFalse();
    }

    [Fact]
    public async Task AnonymousCameraApiStaysUnauthorized()
    {
        var response = await _client.GetAsync("/api/cameras");

        response.StatusCode.Must().Be(HttpStatusCode.Unauthorized);
    }

    public sealed class App : IDisposable
    {
        private readonly string _root;
        private readonly WebApplicationFactory<Program> _factory;

        public App()
        {
            _root = Path.Combine(Path.GetTempPath(), "nidus-spa-" + Guid.NewGuid().ToString("N"));
            var webRoot = Path.Combine(_root, "wwwroot");
            Directory.CreateDirectory(webRoot);
            File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>Nidus shell</title>");
            File.WriteAllBytes(Path.Combine(webRoot, "favicon.ico"), [0, 0, 1, 0]);

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(_root);
                builder.UseWebRoot(webRoot);
                builder.UseSetting("Storage:DataDirectory", Path.Combine(_root, "data"));
                builder.UseSetting("Storage:RecordingsDirectory", Path.Combine(_root, "recordings"));
            });
            Client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        public HttpClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
