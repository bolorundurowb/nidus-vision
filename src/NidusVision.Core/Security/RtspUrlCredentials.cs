namespace NidusVision.Core.Security;

public readonly record struct RtspUrlParts(string UrlWithoutCredentials, string? Username, string? Password);

public static class RtspUrlCredentials
{
    public static RtspUrlParts Split(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var trimmed = url.Trim();
        var schemeSep = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeSep < 0)
        {
            return new RtspUrlParts(trimmed, null, null);
        }

        var rest = trimmed[(schemeSep + 3)..];
        var slash = rest.IndexOf('/');
        var authority = slash < 0 ? rest : rest[..slash];
        var path = slash < 0 ? "" : rest[slash..];
        var at = authority.LastIndexOf('@');
        if (at < 0)
        {
            return new RtspUrlParts(trimmed, null, null);
        }

        var userInfo = authority[..at];
        var host = authority[(at + 1)..];
        string? username = userInfo;
        string? password = null;
        var colon = userInfo.IndexOf(':');
        if (colon >= 0)
        {
            username = userInfo[..colon];
            password = userInfo[(colon + 1)..];
        }

        return new RtspUrlParts(
            string.Concat(trimmed.AsSpan(0, schemeSep + 3), host, path),
            Decode(username),
            Decode(password));
    }

    public static string Redact(string url) => Display(url, hasStoredCredentials: false);

    public static string Display(string url, bool hasStoredCredentials)
    {
        var parts = Split(url);
        var hasUserInfo = parts.Username is not null || parts.Password is not null;
        if (!hasUserInfo && !hasStoredCredentials)
        {
            return parts.UrlWithoutCredentials;
        }

        var schemeSep = parts.UrlWithoutCredentials.IndexOf("://", StringComparison.Ordinal);
        if (schemeSep < 0)
        {
            return parts.UrlWithoutCredentials;
        }

        return string.Concat(parts.UrlWithoutCredentials.AsSpan(0, schemeSep + 3), "***@", parts.UrlWithoutCredentials.AsSpan(schemeSep + 3));
    }

    public static string? MeaningfulUserInfo(string? value) =>
        string.IsNullOrWhiteSpace(value) || value is "***" ? null : value;

    private static string? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
