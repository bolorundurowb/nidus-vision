using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web.Cameras;

namespace NidusVision.Tests;

public sealed class CameraCredentialsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public CameraCredentialsTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Blank_credentials_leave_stored_values()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(Write("Front", "user", "secret"), CancellationToken.None);

        await service.UpdateAsync(created.Id, Write("Front", null, null), CancellationToken.None);
        var camera = await db.Cameras.AsNoTracking().SingleAsync();
        camera.Username.Must().Be("user");
        camera.PasswordProtected.Must().NotBeNull();
    }

    [Fact]
    public async Task ClearCredentials_removes_stored_username_and_password()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(Write("Front", "user", "secret"), CancellationToken.None);

        var updated = await service.UpdateAsync(created.Id, Write("Front", null, null, clear: true), CancellationToken.None);
        updated!.HasPassword.Must().BeFalse();
        var camera = await db.Cameras.AsNoTracking().SingleAsync();
        camera.Username.VerifyNullable().BeNull();
        camera.PasswordProtected.VerifyNullable().BeNull();
    }

    private CameraService CreateService(AppDbContext db) =>
        new(db, new EphemeralDataProtectionProvider(), new RtspProbe(), TimeProvider.System);

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static CameraWriteRequest Write(string name, string? username, string? password, bool clear = false) =>
        new(name, "Yard", true, "rtsp://192.168.1.20/stream", null, username, password, "tcp", null, clear);
}
