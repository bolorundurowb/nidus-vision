using Microsoft.AspNetCore.Mvc;
using NidusVision.Core.Contracts;

namespace NidusVision.Web.Cameras;

internal static class CameraEndpoints
{
    public static void MapCameraEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cameras").RequireAuthorization();
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/probe", Probe);
    }

    private static async Task<IResult> List(CameraService cameras, CancellationToken cancellationToken) =>
        TypedResults.Ok(await cameras.ListAsync(cancellationToken));

    private static async Task<IResult> Get(Guid id, CameraService cameras, CancellationToken cancellationToken)
    {
        var camera = await cameras.GetAsync(id, cancellationToken);
        return camera is null ? TypedResults.NotFound() : TypedResults.Ok(camera);
    }

    private static async Task<IResult> Create(CameraService cameras, [FromBody] CameraWriteRequest request, CancellationToken cancellationToken) =>
        TypedResults.Created($"/api/cameras", await cameras.CreateAsync(request, cancellationToken));

    private static async Task<IResult> Update(Guid id, CameraService cameras, [FromBody] CameraWriteRequest request, CancellationToken cancellationToken)
    {
        var camera = await cameras.UpdateAsync(id, request, cancellationToken);
        return camera is null ? TypedResults.NotFound() : TypedResults.Ok(camera);
    }

    private static async Task<IResult> Delete(Guid id, CameraService cameras, CancellationToken cancellationToken) =>
        await cameras.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<IResult> Probe(CameraService cameras, [FromBody] CameraWriteRequest request, CancellationToken cancellationToken) =>
        TypedResults.Ok(await cameras.ProbeAsync(request, cancellationToken));
}
