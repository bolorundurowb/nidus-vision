using NidusVision.Data;
using NidusVision.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNidusData(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.InitializeNidusDatabaseAsync();

app.MapNidusHealth();

app.Run();
