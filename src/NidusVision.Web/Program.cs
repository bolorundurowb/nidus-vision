using NidusVision.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapNidusHealth();

app.Run();
