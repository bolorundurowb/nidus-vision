using NidusVision.Data;
using NidusVision.Inference;
using NidusVision.Streaming;
using NidusVision.Web;
using NidusVision.Web.Auth;
using NidusVision.Web.Cameras;
using NidusVision.Web.Events;
using NidusVision.Web.Hubs;
using NidusVision.Web.Inference;
using NidusVision.Web.Ingest;
using NidusVision.Web.Live;
using NidusVision.Web.Timeline;
using NidusVision.Web.Settings;

var builder = WebApplication.CreateBuilder(args);
var legacyEventsDirectory = builder.Configuration["Storage:EventsDirectory"] ?? "events";

builder.Services.AddNidusData(builder.Configuration);
builder.Services.AddNidusAuth();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<RtspProbe>();
builder.Services.AddScoped<CameraService>();
builder.Services.AddScoped<LiveStreamService>();
builder.Services.AddSingleton<ProcessCpuSampler>();
builder.Services.AddSingleton<StorageMetricsCache>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<TimelineService>();
builder.Services.AddScoped<RecordingLibraryService>();
builder.Services.AddSingleton<VideoThumbnailExtractor>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ReconnectBackoff>();
builder.Services.AddSingleton<FfmpegSegmentProcess>();
builder.Services.AddSingleton<CameraStatusTracker>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<DetectionFrameBroker>();
builder.Services.AddSingleton(sp => new HumanDetector(
    sp.GetRequiredService<ILogger<HumanDetector>>(),
    sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath));
builder.Services.AddSingleton<IHumanDetector>(sp => sp.GetRequiredService<HumanDetector>());
builder.Services.AddHostedService<DetectionHostedService>();
builder.Services.AddHostedService<CameraIngestHostedService>();
builder.Services.AddHostedService<ThumbnailWorker>();
builder.Services.AddHostedService<RetentionWorker>();
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.InitializeNidusDatabaseAsync();
CleanupLegacyEventsDirectory(
    app.Environment.ContentRootPath,
    legacyEventsDirectory,
    builder.Configuration["Storage:DataDirectory"] ?? "data",
    builder.Configuration["Storage:RecordingsDirectory"] ?? "recordings",
    app.Logger);

if (FfmpegExecutable.IsAvailable())
{
    app.Logger.LogInformation("Using FFmpeg at {FfmpegPath}", FfmpegExecutable.FileName);
}
else
{
    app.Logger.LogWarning(
        "FFmpeg was not found at {FfmpegPath}. Ingest, probe, and live require the Docker image (or FFMPEG_PATH / FFmpeg on PATH for local development).",
        FfmpegExecutable.FileName);
}

// wwwroot is populated by the Angular production/Docker build. During local API-only
// `dotnet run`, the SPA is served from ng serve (port 4200) and this folder is absent.
// Serve those files before authorization. The fallback policy would otherwise reject
// hashed scripts and styles, which do not match the extensionless anonymous SPA fallback.
var webRootExists = Directory.Exists(app.Environment.WebRootPath);
if (webRootExists)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapNidusHealth();
app.MapAuthEndpoints();
app.MapCameraEndpoints();
app.MapSettingsEndpoints();
app.MapLiveEndpoints();
app.MapTimelineEndpoints();
app.MapRecordingEndpoints();
app.MapHub<CameraStatusHub>("/hubs/status");
if (webRootExists)
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Run();

static void CleanupLegacyEventsDirectory(
    string contentRoot,
    string configuredPath,
    string dataPath,
    string recordingsPath,
    ILogger logger)
{
    var root = Path.GetFullPath(contentRoot);
    var legacy = Path.GetFullPath(configuredPath, root);
    var data = Path.GetFullPath(dataPath, root);
    var recordings = Path.GetFullPath(recordingsPath, root);
    var volumeRoot = Path.GetPathRoot(legacy);
    if (string.Equals(legacy, root, StringComparison.OrdinalIgnoreCase)
        || string.Equals(legacy, volumeRoot, StringComparison.OrdinalIgnoreCase)
        || string.Equals(legacy, data, StringComparison.OrdinalIgnoreCase)
        || string.Equals(legacy, recordings, StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("Skipped unsafe legacy event directory cleanup for {EventsDirectory}.", legacy);
        return;
    }

    if (Directory.Exists(legacy))
    {
        try
        {
            var removed = 0;
            foreach (var path in Directory.EnumerateFiles(legacy, "*", SearchOption.AllDirectories)
                         .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".jpg" or ".jpeg"))
            {
                File.Delete(path);
                removed++;
            }

            foreach (var directory in Directory.EnumerateDirectories(legacy, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }

            if (!Directory.EnumerateFileSystemEntries(legacy).Any())
            {
                Directory.Delete(legacy);
            }

            logger.LogInformation(
                "Removed {ArtifactCount} legacy event artifacts from {EventsDirectory}.",
                removed,
                legacy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not remove all legacy event artifacts from {EventsDirectory}.", legacy);
        }
    }
}

public partial class Program;
