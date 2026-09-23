using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web;
using NidusVision.Web.Auth;
using NidusVision.Web.Cameras;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNidusData(builder.Configuration);
builder.Services.AddNidusAuth();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<RtspProbe>();
builder.Services.AddScoped<CameraService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.InitializeNidusDatabaseAsync();

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapNidusHealth();
app.MapAuthEndpoints();
app.MapCameraEndpoints();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
