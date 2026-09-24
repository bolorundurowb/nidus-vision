using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web.Cameras;

namespace NidusVision.Tests;

public sealed class CameraCredentialsTests : SqliteTestBase
{
    [Fact]
    public async Task UpdateAsyncWithBlankCredentialsPreservesStoredCredentials()
    {
        // Arrange
        await using var db = CreateContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(Write("Front", "user", "secret"), CancellationToken.None);

        // Act
        await service.UpdateAsync(created.Id, Write("Front", null, null), CancellationToken.None);
        var camera = await db.Cameras.AsNoTracking().SingleAsync();

        // Assert
        camera.Username.Must().Be("user");
        camera.PasswordProtected.Must().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsyncWhenClearCredentialsIsRequestedRemovesStoredCredentials()
    {
        // Arrange
        await using var db = CreateContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(Write("Front", "user", "secret"), CancellationToken.None);

        // Act
        var updated = await service.UpdateAsync(created.Id, Write("Front", null, null, clear: true), CancellationToken.None);
        var camera = await db.Cameras.AsNoTracking().SingleAsync();

        // Assert
        updated!.HasPassword.Must().BeFalse();
        camera.Username.VerifyNullable().BeNull();
        camera.PasswordProtected.VerifyNullable().BeNull();
    }

    private CameraService CreateService(AppDbContext db) =>
        new(db, new EphemeralDataProtectionProvider(), new RtspProbe(), TimeProvider.System);

    private static CameraWriteRequest Write(string name, string? username, string? password, bool clear = false) =>
        new(name, "Yard", true, "rtsp://192.168.1.20/stream", null, username, password, "tcp", null, clear);
}
