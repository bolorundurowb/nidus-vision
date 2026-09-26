using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
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

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://192.168.1.20/stream")]
    [InlineData("concat:/tmp/a.mp4|/tmp/b.mp4")]
    [InlineData("/var/nidus/recordings/a.mp4")]
    public async Task CreateAsyncRejectsNonRtspUrls(string url)
    {
        // Arrange
        await using var db = CreateContext();
        var service = CreateService(db);

        // Act
        var rejected = false;
        try
        {
            await service.CreateAsync(Write("Front", null, null, url: url), CancellationToken.None);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        // Assert
        rejected.Must().BeTrue();
        (await db.Cameras.CountAsync()).Must().Be(0);
    }

    [Fact]
    public async Task CreateAsyncAcceptsRtspsWithCredentials()
    {
        // Arrange
        await using var db = CreateContext();
        var service = CreateService(db);

        // Act
        var created = await service.CreateAsync(
            Write("Front", null, null, url: "rtsps://admin:p@ss@cam.local:322/live"),
            CancellationToken.None);

        // Assert
        created.HasPassword.Must().BeTrue();
    }

    [Fact]
    public void ResolveRtspUrlRefusesStoredNonRtspUrl()
    {
        // Arrange
        using var db = CreateContext();
        var service = CreateService(db);
        var camera = new Camera { Name = "Legacy", MainRtspUrl = "file:///etc/passwd" };

        // Act
        var refused = false;
        try
        {
            service.ResolveRtspUrl(camera);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        // Assert
        refused.Must().BeTrue();
    }

    [Fact]
    public async Task DeleteAsyncRemovesTheCameraRecordingsDirectory()
    {
        // Arrange
        using var directory = new TestDirectory();
        var recordings = directory.GetPath("recordings");
        await using var db = CreateContext();
        var service = CreateService(db, recordings);
        var created = await service.CreateAsync(Write("Front", null, null), CancellationToken.None);
        var cameraDirectory = Path.Combine(recordings, created.Id.ToString("N"));
        var otherDirectory = Path.Combine(recordings, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cameraDirectory);
        Directory.CreateDirectory(otherDirectory);
        await File.WriteAllBytesAsync(Path.Combine(cameraDirectory, "20260923T183000.mp4"), [1, 2, 3]);

        // Act
        var deleted = await service.DeleteAsync(created.Id, CancellationToken.None);

        // Assert
        deleted.Must().BeTrue();
        Directory.Exists(cameraDirectory).Must().BeFalse();
        Directory.Exists(otherDirectory).Must().BeTrue();
    }

    private CameraService CreateService(AppDbContext db, string? recordingsDirectory = null) =>
        new(
            db,
            new EphemeralDataProtectionProvider(),
            new RtspProbe(),
            TimeProvider.System,
            Options.Create(new StorageOptions { RecordingsDirectory = recordingsDirectory ?? "recordings" }),
            NullLogger<CameraService>.Instance);

    private static CameraWriteRequest Write(
        string name,
        string? username,
        string? password,
        bool clear = false,
        string url = "rtsp://192.168.1.20/stream") =>
        new(name, "Interior", true, url, null, username, password, "tcp", clear);
}
