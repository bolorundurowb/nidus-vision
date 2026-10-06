using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace NidusVision.Web.Auth;

/// <summary>
/// Trust <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> from a TLS reverse proxy (Caddy, nginx,
/// Traefik), so the login throttle sees the real client and the auth cookie gets the Secure flag
/// over HTTPS. Loopback proxies are trusted by default. Headers from any other address are
/// ignored unless it is listed under <c>ReverseProxy:KnownProxies</c> or
/// <c>ReverseProxy:KnownNetworks</c>.
/// </summary>
internal static class ReverseProxySupport
{
    public const string SectionName = "ReverseProxy";

    public static IServiceCollection AddNidusReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var proxies = ReadList(section.GetSection("KnownProxies"))
            .Select(value => IPAddress.TryParse(value, out var address)
                ? address
                : throw new InvalidOperationException($"{SectionName}:KnownProxies entry '{value}' is not an IP address."))
            .ToList();
        var networks = ReadList(section.GetSection("KnownNetworks"))
            .Select(value => System.Net.IPNetwork.TryParse(value, out var network)
                ? network
                : throw new InvalidOperationException($"{SectionName}:KnownNetworks entry '{value}' is not a CIDR range such as 172.16.0.0/12."))
            .ToList();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
        return services;
    }

    /// <summary>
    /// Accepts both an indexed list (<c>ReverseProxy__KnownNetworks__0=...</c>) and one
    /// comma-separated value (<c>ReverseProxy__KnownNetworks=172.16.0.0/12,10.0.0.0/8</c>),
    /// which is easier to write in a compose file.
    /// </summary>
    internal static IReadOnlyList<string> ReadList(IConfigurationSection section)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(section.Value))
        {
            values.AddRange(section.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                values.AddRange(child.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return values;
    }
}
