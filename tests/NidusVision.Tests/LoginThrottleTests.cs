using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NidusVision.Web.Auth;

namespace NidusVision.Tests;

public sealed class LoginThrottleTests
{
    private static readonly IPAddress Client = IPAddress.Parse("192.168.1.20");

    [Fact]
    public async Task ParallelAttemptsCannotExceedTheLimit()
    {
        // Arrange
        var throttle = new LoginThrottle(new ManualTimeProvider());

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => throttle.TryBeginAttempt(Client, out _))));

        // Assert
        results.Count(allowed => allowed).Must().Be(LoginThrottle.MaxFailures);
    }

    [Fact]
    public void LockoutExpiresAfterTheLockoutDuration()
    {
        // Arrange
        var time = new ManualTimeProvider();
        var throttle = new LoginThrottle(time);
        for (var i = 0; i < LoginThrottle.MaxFailures; i++)
        {
            throttle.TryBeginAttempt(Client, out _).Must().BeTrue();
        }

        throttle.TryBeginAttempt(Client, out var retryAfter).Must().BeFalse();
        (retryAfter > TimeSpan.Zero).Must().BeTrue();

        // Act
        time.Advance(LoginThrottle.LockoutDuration + TimeSpan.FromSeconds(1));

        // Assert
        throttle.TryBeginAttempt(Client, out _).Must().BeTrue();
    }

    [Fact]
    public void FailuresDecayAfterTheWindow()
    {
        // Arrange
        var time = new ManualTimeProvider();
        var throttle = new LoginThrottle(time);
        for (var i = 0; i < LoginThrottle.MaxFailures; i++)
        {
            throttle.TryBeginAttempt(Client, out _);
        }

        // Act
        time.Advance(LoginThrottle.FailureWindow + TimeSpan.FromSeconds(1));

        // Assert
        throttle.TryBeginAttempt(Client, out _).Must().BeTrue();
    }

    [Fact]
    public void SuccessReleasesTheClient()
    {
        // Arrange
        var throttle = new LoginThrottle(new ManualTimeProvider());
        for (var i = 0; i < LoginThrottle.MaxFailures; i++)
        {
            throttle.TryBeginAttempt(Client, out _);
        }

        // Act
        throttle.RecordSuccess(Client);

        // Assert
        throttle.TryBeginAttempt(Client, out _).Must().BeTrue();
    }

    [Fact]
    public void ClientsAreTrackedSeparately()
    {
        // Arrange
        var throttle = new LoginThrottle(new ManualTimeProvider());
        for (var i = 0; i <= LoginThrottle.MaxFailures; i++)
        {
            throttle.TryBeginAttempt(Client, out _);
        }

        // Act & Assert
        throttle.TryBeginAttempt(Client, out _).Must().BeFalse();
        throttle.TryBeginAttempt(IPAddress.Parse("192.168.1.21"), out _).Must().BeTrue();
    }

    [Fact]
    public void Ipv6ClientsInOnePrefixShareABucket()
    {
        // Act
        var first = LoginThrottle.Key(IPAddress.Parse("2001:db8:1:2::10"));
        var second = LoginThrottle.Key(IPAddress.Parse("2001:db8:1:2:ffff::99"));
        var other = LoginThrottle.Key(IPAddress.Parse("2001:db8:1:3::10"));

        // Assert
        first.Must().Be(second);
        first.Must().NotBe(other);
    }

    [Fact]
    public void Ipv4MappedAddressesUseTheIpv4Key()
    {
        LoginThrottle.Key(IPAddress.Parse("::ffff:192.168.1.20")).Must().Be("192.168.1.20");
    }

    [Fact]
    public void ReverseProxySettingsAddTrustedProxiesAndNetworks()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:KnownProxies:0"] = "10.0.0.2",
                ["ReverseProxy:KnownNetworks"] = "172.16.0.0/12, 192.168.0.0/16",
            })
            .Build();
        var services = new ServiceCollection();

        // Act
        services.AddNidusReverseProxy(configuration);
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // Assert
        options.KnownProxies.Contains(IPAddress.Parse("10.0.0.2")).Must().BeTrue();
        options.KnownIPNetworks.Contains(System.Net.IPNetwork.Parse("172.16.0.0/12")).Must().BeTrue();
        options.KnownIPNetworks.Contains(System.Net.IPNetwork.Parse("192.168.0.0/16")).Must().BeTrue();
    }

    [Fact]
    public void ReverseProxySettingsRejectInvalidEntries()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReverseProxy:KnownNetworks"] = "not-a-network" })
            .Build();

        // Act
        InvalidOperationException? caught = null;
        try
        {
            new ServiceCollection().AddNidusReverseProxy(configuration);
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        // Assert
        (caught is not null).Must().BeTrue();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
