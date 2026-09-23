namespace NidusVision.Web;

internal static class HealthEndpoints
{
    public static void MapNidusHealth(this IEndpointRouteBuilder app) =>
        app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
            .AllowAnonymous()
            .WithName("Health");
}

internal sealed record HealthResponse(string Status);
