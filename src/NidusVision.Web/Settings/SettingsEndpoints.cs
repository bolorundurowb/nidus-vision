using Microsoft.AspNetCore.Mvc;
using NidusVision.Core.Contracts;

namespace NidusVision.Web.Settings;

internal static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", async (SettingsService settings, CancellationToken cancellationToken) =>
            TypedResults.Ok(await settings.GetAsync(cancellationToken)));
        app.MapPut("/api/settings", async (SettingsService settings, [FromBody] SettingsWriteRequest request, CancellationToken cancellationToken) =>
            TypedResults.Ok(await settings.UpdateAsync(request, cancellationToken)));
        app.MapGet("/api/system/metrics", (SettingsService settings) => TypedResults.Ok(settings.GetSystemMetrics()));
    }
}
