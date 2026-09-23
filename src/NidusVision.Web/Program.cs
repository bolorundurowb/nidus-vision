using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web;
using NidusVision.Web.Auth;
using NidusVision.Web.Cameras;
using NidusVision.Web.Ingest;
using NidusVision.Web.Live;
using NidusVision.Web.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNidusData(builder.Configuration);
builder.Services.AddNidusAuth();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<RtspProbe>();
builder.Services.AddScoped<CameraService>();
builder.Services.AddScoped<LiveStreamService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ReconnectBackoff>();
builder.Services.AddSingleton<FfmpegSegmentProcess>();
builder.Services.AddHostedService<CameraIngestHostedService>();

var app = builder.Build();

await app.InitializeNidusDatabaseAsync();

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapNidusHealth();
app.MapAuthEndpoints();
app.MapCameraEndpoints();
app.MapSettingsEndpoints();
app.MapLiveEndpoints();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
