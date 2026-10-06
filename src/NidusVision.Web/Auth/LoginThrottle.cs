using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace NidusVision.Web.Auth;

/// <summary>
/// In-memory per-client lockout after repeated failed logins. Restarting the process clears it.
/// </summary>
/// <remarks>
/// A slot is reserved before the password is checked, so parallel requests cannot all get past
/// the limit while the hash is being verified. Only a successful login releases the reservation.
/// Counts decay after <see cref="FailureWindow"/>. Expired entries are pruned so the table cannot
/// grow without bound. IPv6 clients are grouped by /64, because one host can hold a whole prefix.
/// </remarks>
public sealed class LoginThrottle(TimeProvider time)
{
    public const int MaxFailures = 5;
    public const int MaxTrackedClients = 10_000;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    private const string OverflowKey = "overflow";
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>
    /// Reserves one login attempt for <paramref name="client"/>. Returns false, with the time
    /// left, when the client is locked out. Call <see cref="RecordSuccess"/> after a correct password.
    /// </summary>
    public bool TryBeginAttempt(IPAddress? client, out TimeSpan retryAfter)
    {
        var now = time.GetUtcNow();
        var key = KeyFor(client, now);
        var entry = _entries.AddOrUpdate(
            key,
            _ => new Entry(1, now, null),
            (_, current) => Next(current, now));

        if (entry.LockedUntil is { } until && until > now)
        {
            retryAfter = until - now;
            return false;
        }

        retryAfter = TimeSpan.Zero;
        return true;
    }

    public void RecordSuccess(IPAddress? client) => _entries.TryRemove(Key(client), out _);

    internal int TrackedClients => _entries.Count;

    // State machine for a client's entry:
    //   [Active counting] --(attempts > MaxFailures)--> [Locked out]
    //   [Locked out]      --(now >= LockedUntil)-->     [Active counting] (reset to 1)
    //   [Active counting] --(now - WindowStart > FailureWindow)--> [Active counting] (reset to 1)
    //   [Locked out]      --(now < LockedUntil)-->       [Locked out] (unchanged)
    // The current attempt is always counted (Attempts starts at 1 on reset), so the caller
    // never needs to increment after the fact.
    private static Entry Next(Entry current, DateTimeOffset now)
    {
        if (current.LockedUntil is { } until)
        {
            return until > now ? current : new Entry(1, now, null);
        }

        if (now - current.WindowStart > FailureWindow)
        {
            return new Entry(1, now, null);
        }

        var attempts = current.Attempts + 1;
        return attempts > MaxFailures
            ? current with { Attempts = attempts, LockedUntil = now + LockoutDuration }
            : current with { Attempts = attempts };
    }

    private string KeyFor(IPAddress? client, DateTimeOffset now)
    {
        var key = Key(client);
        if (_entries.ContainsKey(key) || _entries.Count < MaxTrackedClients)
        {
            return key;
        }

        Prune(now);
        // Still full after pruning: new clients share one bucket instead of growing the table.
        // Tradeoff: an attacker with many source IPs could fill the table and push legitimate
        // new clients into the shared overflow bucket, where they might be throttled together.
        // Acceptable for a self-hosted NVR behind a trusted proxy; revisit if multi-tenant.
        return _entries.Count < MaxTrackedClients ? key : OverflowKey;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, entry) in _entries)
        {
            var expired = entry.LockedUntil is { } until
                ? until <= now
                : now - entry.WindowStart > FailureWindow;
            if (expired)
            {
                _entries.TryRemove(key, out _);
            }
        }
    }

    internal static string Key(IPAddress? client)
    {
        if (client is null)
        {
            return "unknown";
        }

        if (client.IsIPv4MappedToIPv6)
        {
            return client.MapToIPv4().ToString();
        }

        if (client.AddressFamily == AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(client))
        {
            var bytes = client.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes) + "/64";
        }

        return client.ToString();
    }

    private sealed record Entry(int Attempts, DateTimeOffset WindowStart, DateTimeOffset? LockedUntil);
}
