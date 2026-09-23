using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NidusVision.Data;
using NidusVision.Web.Cameras;

namespace NidusVision.Web.Live;

public sealed class LiveStreamService(AppDbContext db, CameraService cameras)
{
    public async Task StreamAsync(Guid cameraId, Stream output, CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cameraId, cancellationToken)
            ?? throw new FileNotFoundException("Camera not found.");
        var password = cameras.UnprotectPassword(camera);
        var url = camera.MainRtspUrl;
        if (!string.IsNullOrWhiteSpace(camera.Username) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            url = new UriBuilder(uri) { UserName = camera.Username, Password = password ?? "" }.Uri.ToString();
        }

        var transport = camera.Transport.ToString().ToLowerInvariant();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList =
                {
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-rtsp_transport",
                    transport,
                    "-i",
                    url,
                    "-an",
                    "-c",
                    "copy",
                    "-f",
                    "mp4",
                    "-movflags",
                    "frag_keyframe+empty_moov+default_base_moof",
                    "pipe:1",
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal static class LiveEndpoints
{
    public static void MapLiveEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/cameras/{id:guid}/live", async (Guid id, LiveStreamService live, HttpContext http, CancellationToken cancellationToken) =>
        {
            http.Response.ContentType = "video/mp4";
            http.Response.Headers.CacheControl = "no-store";
            try
            {
                await live.StreamAsync(id, http.Response.Body, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
            }
        });
}
