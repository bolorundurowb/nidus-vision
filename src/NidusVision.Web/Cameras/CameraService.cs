using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Cameras;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Core.Security;
using NidusVision.Data;
using NidusVision.Streaming;

namespace NidusVision.Web.Cameras;

/// <summary>Per-camera numbers measured from recorded segments; null means "no data yet".</summary>
public sealed record CameraStatsSnapshot(string? Bitrate, string? Retention)
{
    public static readonly CameraStatsSnapshot Empty = new(null, null);
}

public sealed class CameraService(AppDbContext db, IDataProtectionProvider protection, RtspProbe probe, TimeProvider clock)
{
    /// <summary>Newest segments used to average the bitrate; enough to smooth a partial segment.</summary>
    private const int BitrateSampleSegments = 3;

    private static readonly TimeSpan BitrateSampleWindow = TimeSpan.FromHours(24);

    private readonly IDataProtector _protector = protection.CreateProtector("nidus.camera.password");

    public async Task<IReadOnlyList<CameraResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var cameras = await db.Cameras.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var stats = await LoadStatsAsync([.. cameras.Select(c => c.Id)], cancellationToken);
        return [.. cameras.Select(c => ToResponse(c, stats.GetValueOrDefault(c.Id, CameraStatsSnapshot.Empty)))];
    }

    public async Task<CameraResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (camera is null)
        {
            return null;
        }

        var stats = await LoadStatsAsync([camera.Id], cancellationToken);
        return ToResponse(camera, stats.GetValueOrDefault(camera.Id, CameraStatsSnapshot.Empty));
    }

    public async Task<CameraResponse> CreateAsync(CameraWriteRequest request, CancellationToken cancellationToken)
    {
        var camera = Apply(new Camera { Name = request.Name, MainRtspUrl = request.MainRtspUrl }, request);
        db.Cameras.Add(camera);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(camera, CameraStatsSnapshot.Empty);
    }

    public async Task<CameraResponse?> UpdateAsync(Guid id, CameraWriteRequest request, CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (camera is null)
        {
            return null;
        }

        Apply(camera, request);
        camera.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var stats = await LoadStatsAsync([camera.Id], cancellationToken);
        return ToResponse(camera, stats.GetValueOrDefault(camera.Id, CameraStatsSnapshot.Empty));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (camera is null)
        {
            return false;
        }

        db.Cameras.Remove(camera);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<ProbeResult> ProbeAsync(CameraWriteRequest request, CancellationToken cancellationToken)
    {
        var parts = RtspUrlCredentials.Split(request.MainRtspUrl);
        var username = RtspUrlCredentials.MeaningfulUserInfo(parts.Username)
            ?? RtspUrlCredentials.MeaningfulUserInfo(request.Username);
        var password = RtspUrlCredentials.MeaningfulUserInfo(parts.Password)
            ?? RtspUrlCredentials.MeaningfulUserInfo(request.Password);
        return probe.ProbeAsync(BuildUrl(parts.UrlWithoutCredentials, username, password), request.Transport, cancellationToken);
    }

    public string? UnprotectPassword(Camera camera) =>
        camera.PasswordProtected is null ? null : _protector.Unprotect(camera.PasswordProtected);

    public string ResolveRtspUrl(Camera camera)
    {
        var parts = RtspUrlCredentials.Split(camera.MainRtspUrl);
        var username = RtspUrlCredentials.MeaningfulUserInfo(camera.Username)
            ?? RtspUrlCredentials.MeaningfulUserInfo(parts.Username);
        var password = UnprotectPassword(camera)
            ?? RtspUrlCredentials.MeaningfulUserInfo(parts.Password);
        return BuildUrl(parts.UrlWithoutCredentials, username, password);
    }

    private Camera Apply(Camera camera, CameraWriteRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MainRtspUrl);

        var main = RtspUrlCredentials.Split(request.MainRtspUrl);
        camera.Name = request.Name.Trim();
        camera.Location = string.IsNullOrWhiteSpace(request.Location) ? "Unassigned" : request.Location.Trim();
        camera.Enabled = request.Enabled;
        camera.MainRtspUrl = main.UrlWithoutCredentials;
        camera.SubRtspUrl = string.IsNullOrWhiteSpace(request.SubRtspUrl)
            ? null
            : RtspUrlCredentials.Split(request.SubRtspUrl).UrlWithoutCredentials;

        var username = RtspUrlCredentials.MeaningfulUserInfo(main.Username)
            ?? RtspUrlCredentials.MeaningfulUserInfo(request.Username);
        var password = RtspUrlCredentials.MeaningfulUserInfo(main.Password)
            ?? RtspUrlCredentials.MeaningfulUserInfo(request.Password);
        if (username is not null)
        {
            camera.Username = username;
        }

        if (password is not null)
        {
            camera.PasswordProtected = _protector.Protect(password);
        }

        camera.Transport = request.Transport.Equals("udp", StringComparison.OrdinalIgnoreCase)
            ? RtspTransport.Udp
            : RtspTransport.Tcp;
        camera.RoiJson = request.RoiJson;
        return camera;
    }

    public async Task<Dictionary<Guid, CameraStatsSnapshot>> LoadStatsAsync(
        IReadOnlyList<Guid> cameraIds,
        CancellationToken cancellationToken)
    {
        if (cameraIds.Count == 0)
        {
            return [];
        }

        var now = clock.GetUtcNow();
        var since = now - BitrateSampleWindow;
        var oldest = await db.RecordingSegments.AsNoTracking()
            .Where(s => cameraIds.Contains(s.CameraId))
            .GroupBy(s => s.CameraId)
            .Select(g => new { CameraId = g.Key, OldestStartUtc = g.Min(s => s.StartUtc) })
            .ToDictionaryAsync(g => g.CameraId, g => g.OldestStartUtc, cancellationToken);
        var recent = await db.RecordingSegments.AsNoTracking()
            .Where(s => cameraIds.Contains(s.CameraId) && s.StartUtc >= since)
            .OrderByDescending(s => s.StartUtc)
            .Select(s => new { s.CameraId, s.StartUtc, s.EndUtc, s.ByteSize, s.Path })
            .ToListAsync(cancellationToken);
        var samples = recent
            .GroupBy(s => s.CameraId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Take(BitrateSampleSegments)
                    .Select(s => new SegmentStatSample(s.StartUtc, s.EndUtc, SegmentBytes(s.Path, s.ByteSize)))
                    .ToList());

        var stats = new Dictionary<Guid, CameraStatsSnapshot>(cameraIds.Count);
        foreach (var id in cameraIds)
        {
            var bitrate = samples.TryGetValue(id, out var cameraSamples)
                ? CameraStreamStats.Bitrate(cameraSamples, now)
                : null;
            var retention = CameraStreamStats.Retention(
                oldest.TryGetValue(id, out var start) ? start : null,
                now);
            stats[id] = new CameraStatsSnapshot(bitrate, retention);
        }

        return stats;
    }

    /// <summary>The newest segment is still being written, so its indexed size is stale.</summary>
    private static long SegmentBytes(string path, long indexedBytes)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : indexedBytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return indexedBytes;
        }
    }

    private CameraResponse ToResponse(Camera camera, CameraStatsSnapshot stats)
    {
        var main = RtspUrlCredentials.Split(camera.MainRtspUrl);
        var hasPassword = camera.PasswordProtected is not null
            || camera.Username is not null
            || main.Username is not null
            || main.Password is not null;
        return new(
            camera.Id,
            camera.Name,
            camera.Location,
            camera.Enabled,
            RtspUrlCredentials.Display(camera.MainRtspUrl, hasPassword),
            camera.SubRtspUrl is null ? null : RtspUrlCredentials.Display(camera.SubRtspUrl, hasPassword),
            null,
            hasPassword,
            camera.Transport.ToString().ToLowerInvariant(),
            camera.Status.ToString().ToLowerInvariant(),
            camera.RoiJson,
            camera.LastResolution,
            camera.LastFps,
            stats.Bitrate,
            stats.Retention);
    }

    private static string BuildUrl(string url, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var builder = new UriBuilder(uri) { UserName = username, Password = password ?? "" };
        return builder.Uri.ToString();
    }
}
