using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Core.Security;
using NidusVision.Data;
using NidusVision.Streaming;

namespace NidusVision.Web.Cameras;

public sealed class CameraService(AppDbContext db, IDataProtectionProvider protection, RtspProbe probe)
{
    private readonly IDataProtector _protector = protection.CreateProtector("nidus.camera.password");

    public async Task<IReadOnlyList<CameraResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var cameras = await db.Cameras.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return [.. cameras.Select(ToResponse)];
    }

    public async Task<CameraResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return camera is null ? null : ToResponse(camera);
    }

    public async Task<CameraResponse> CreateAsync(CameraWriteRequest request, CancellationToken cancellationToken)
    {
        var camera = Apply(new Camera { Name = request.Name, MainRtspUrl = request.MainRtspUrl }, request);
        db.Cameras.Add(camera);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(camera);
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
        return ToResponse(camera);
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

    private CameraResponse ToResponse(Camera camera)
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
            null,
            null,
            null);
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
