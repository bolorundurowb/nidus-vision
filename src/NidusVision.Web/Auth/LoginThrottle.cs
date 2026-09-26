using System.Collections.Concurrent;
using System.Net;

namespace NidusVision.Web.Auth;

/// <summary>In-memory per-client lockout after repeated failed logins. Restarting the process clears it.</summary>
public sealed class LoginThrottle(TimeProvider time)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public bool IsLockedOut(IPAddress? client, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_entries.TryGetValue(Key(client), out var entry) || entry.LockedUntil is not { } until)
        {
            return false;
        }

        var remaining = until - time.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            _entries.TryRemove(Key(client), out _);
            return false;
        }

        retryAfter = remaining;
        return true;
    }

    public void RecordFailure(IPAddress? client) =>
        _entries.AddOrUpdate(
            Key(client),
            _ => new Entry(1, null),
            (_, entry) =>
            {
                var failures = entry.Failures + 1;
                return failures >= MaxFailures
                    ? new Entry(0, time.GetUtcNow() + LockoutDuration)
                    : entry with { Failures = failures };
            });

    public void RecordSuccess(IPAddress? client) => _entries.TryRemove(Key(client), out _);

    private static string Key(IPAddress? client) => client?.ToString() ?? "unknown";

    private sealed record Entry(int Failures, DateTimeOffset? LockedUntil);
}
