using NidusVision.Data;
using NidusVision.Web;
using NidusVision.Web.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNidusData(builder.Configuration);
builder.Services.AddNidusAuth();
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.InitializeNidusDatabaseAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapNidusHealth();
app.MapAuthEndpoints();

app.Run();
