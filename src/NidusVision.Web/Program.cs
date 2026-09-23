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

builder.Services.AddNidusData(builder.Configuration);
builder.Services.AddNidusAuth();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<RtspProbe>();
builder.Services.AddScoped<CameraService>();
builder.Services.AddScoped<LiveStreamService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<TimelineService>();
builder.Services.AddScoped<EventLibraryService>();
builder.Services.AddSingleton<EventArtifactStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ReconnectBackoff>();
builder.Services.AddSingleton<FfmpegSegmentProcess>();
builder.Services.AddSingleton<CameraStatusTracker>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<HumanDetector>();
builder.Services.AddHostedService<DetectionHostedService>();
builder.Services.AddHostedService<CameraIngestHostedService>();
builder.Services.AddHostedService<RetentionWorker>();
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.InitializeNidusDatabaseAsync();

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

app.UseAuthentication();
app.UseAuthorization();

// wwwroot is populated by the Angular production/Docker build. During local API-only
// `dotnet run`, the SPA is served from ng serve (port 4200) and this folder is absent.
var webRootExists = Directory.Exists(app.Environment.WebRootPath);
if (webRootExists)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapNidusHealth();
app.MapAuthEndpoints();
app.MapCameraEndpoints();
app.MapSettingsEndpoints();
app.MapLiveEndpoints();
app.MapTimelineEndpoints();
app.MapEventEndpoints();
app.MapHub<CameraStatusHub>("/hubs/status");
app.MapHub<DetectionHub>("/hubs/detections");
if (webRootExists)
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Run();
