using Microsoft.EntityFrameworkCore;
using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web.Cameras;

namespace NidusVision.Web.Live;

public sealed class RtspStreamException(string message) : Exception(message);

public sealed class LiveStreamService(AppDbContext db, CameraService cameras, ILogger<LiveStreamService> logger)
{
    public async Task StreamAsync(Guid cameraId, Stream output, CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cameraId, cancellationToken)
            ?? throw new FileNotFoundException("Camera not found.");
        if (!camera.Enabled)
        {
            throw new InvalidOperationException("This camera is disabled.");
        }

        var url = cameras.ResolveRtspUrl(camera);

        var transport = camera.Transport.ToString().ToLowerInvariant();
        using var process = FfmpegExecutable.Create(startInfo =>
        {
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-rtsp_transport");
            startInfo.ArgumentList.Add(transport);
            startInfo.ArgumentList.Add("-timeout");
            startInfo.ArgumentList.Add("10000000");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(url);
            startInfo.ArgumentList.Add("-an");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("copy");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("mp4");
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add("frag_keyframe+empty_moov+default_base_moof");
            // Without an explicit fragment duration the first fragment only lands on the next
            // keyframe, which is several seconds of black video on cameras with long GOPs.
            startInfo.ArgumentList.Add("-frag_duration");
            startInfo.ArgumentList.Add("200000");
            startInfo.ArgumentList.Add("-flush_packets");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("pipe:1");
        });
        FfmpegExecutable.Start(process);
        var errors = FfmpegExecutable.CaptureErrors(process);

        long copied = 0;
        try
        {
            var buffer = new byte[64 * 1024];
            var source = process.StandardOutput.BaseStream;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                copied += read;
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            Stop(process);
        }

        if (copied == 0)
        {
            var message = errors.Describe("FFmpeg produced no video for this camera.");
            // FfmpegErrorLog redacts URL credentials as lines arrive, so the raw text is safe to log.
            logger.LogWarning("Live stream for camera {CameraId} produced no data: {Ffmpeg}", cameraId, errors.Text);
            throw new RtspStreamException(message);
        }
    }

    private static void Stop(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            /* already exited or cannot be killed; never mask the error that ended the stream */
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
                await WriteProblem(http, StatusCodes.Status404NotFound, "Camera not found.");
            }
            catch (RtspStreamException ex)
            {
                await WriteProblem(http, StatusCodes.Status502BadGateway, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await WriteProblem(http, StatusCodes.Status503ServiceUnavailable, ex.Message);
            }
        });

    private static async Task WriteProblem(HttpContext http, int statusCode, string message)
    {
        if (http.Response.HasStarted)
        {
            return;
        }

        http.Response.StatusCode = statusCode;
        http.Response.ContentType = "application/json";
        await http.Response.WriteAsJsonAsync(new { message });
    }
}
