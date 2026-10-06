using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Web.Settings;

namespace NidusVision.Tests;

public sealed class SettingsServiceTests : SqliteTestBase
{
    [Theory]
    [InlineData(1_000f, 7f, 5f, 1f)]
    [InlineData(0f, -2f, 0.2f, 0f)]
    [InlineData(2.5f, 0.45f, 2.5f, 0.45f)]
    public async Task UpdateAsyncClampsInferenceSettings(float sampleFps, float confidence, float expectedFps, float expectedConfidence)
    {
        // Arrange
        await using var db = CreateContext();
        db.AppSettings.Add(new AppSettings());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var response = await service.UpdateAsync(Write(sampleFps, confidence), CancellationToken.None);
        var row = await db.AppSettings.AsNoTracking().SingleAsync();

        // Assert
        response.SampleFps.Must().Be(expectedFps);
        response.ConfidenceThreshold.Must().Be(expectedConfidence);
        row.SampleFps.Must().Be(expectedFps);
        row.ConfidenceThreshold.Must().Be(expectedConfidence);
    }

    [Theory]
    [InlineData(float.NaN, 0.5f)]
    [InlineData(float.PositiveInfinity, 0.5f)]
    [InlineData(1f, float.NaN)]
    [InlineData(1f, float.NegativeInfinity)]
    public async Task UpdateAsyncRejectsNonFiniteInferenceSettings(float sampleFps, float confidence)
    {
        // Arrange
        await using var db = CreateContext();
        db.AppSettings.Add(new AppSettings());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var rejected = false;
        try
        {
            await service.UpdateAsync(Write(sampleFps, confidence), CancellationToken.None);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }

        var row = await db.AppSettings.AsNoTracking().SingleAsync();

        // Assert
        rejected.Must().BeTrue();
        row.SampleFps.Must().Be(1f);
        row.ConfidenceThreshold.Must().Be(0.6f);
    }

    [Theory]
    [InlineData(0, 90, null)]
    [InlineData(30, 0, null)]
    [InlineData(RetentionLimits.MaxDays + 1, 90, null)]
    [InlineData(30, int.MaxValue, null)]
    [InlineData(30, 90, 1L)]
    [InlineData(30, 90, RetentionLimits.MinStorageBytes - 1)]
    public async Task UpdateAsyncRejectsOutOfRangeRetention(int generalDays, int detectionDays, long? maxStorageBytes)
    {
        // Arrange
        await using var db = CreateContext();
        db.AppSettings.Add(new AppSettings { MaxStorageBytes = 50L << 30 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var rejected = false;
        try
        {
            await service.UpdateAsync(new SettingsWriteRequest(generalDays, detectionDays, maxStorageBytes, true, 1f, 0.6f, SegmentDurationLimits.DefaultSegmentDurationSeconds), CancellationToken.None);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }

        var row = await db.AppSettings.AsNoTracking().SingleAsync();

        // Assert
        rejected.Must().BeTrue();
        row.GeneralRetentionDays.Must().Be(30);
        row.MaxStorageBytes.Must().Be(50L << 30);
    }

    [Fact]
    public async Task UpdateAsyncAcceptsTheLimits()
    {
        // Arrange
        await using var db = CreateContext();
        db.AppSettings.Add(new AppSettings());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var response = await service.UpdateAsync(
            new SettingsWriteRequest(RetentionLimits.MaxDays, RetentionLimits.MinDays, RetentionLimits.MinStorageBytes, true, 1f, 0.6f, SegmentDurationLimits.MaxSegmentDurationSeconds),
            CancellationToken.None);

        // Assert
        response.GeneralRetentionDays.Must().Be(RetentionLimits.MaxDays);
        response.DetectionRetentionDays.Must().Be(RetentionLimits.MinDays);
        response.MaxStorageBytes.Must().Be(RetentionLimits.MinStorageBytes);
    }

    [Theory]
    [InlineData(5 * 60)]
    [InlineData(15 * 60)]
    [InlineData(30 * 60)]
    public async Task UpdateAsyncAcceptsValidSegmentDurations(int segmentDurationSeconds)
    {
        // Arrange
        await using var db = CreateContext();
        db.AppSettings.Add(new AppSettings());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var response = await service.UpdateAsync(
            new SettingsWriteRequest(30, 90, null, true, 1f, 0.6f, segmentDurationSeconds),
            CancellationToken.None);
        var row = await db.AppSettings.AsNoTracking().SingleAsync();

        // Assert
        response.SegmentDurationSeconds.Must().Be(segmentDurationSeconds);
        row.SegmentDurationSeconds.Must().Be(segmentDurationSeconds);
    }

    [Theory]
    [InlineData(4 * 60)]
    [InlineData(31 * 60)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateAsyncRejectsOutOfRangeSegmentDurations(int segmentDurationSeconds)
    {
        // Arrange
        await using var db = CreateContext();
        db.AppSettings.Add(new AppSettings());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var rejected = false;
        try
        {
            await service.UpdateAsync(
                new SettingsWriteRequest(30, 90, null, true, 1f, 0.6f, segmentDurationSeconds),
                CancellationToken.None);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }

        var row = await db.AppSettings.AsNoTracking().SingleAsync();

        // Assert
        rejected.Must().BeTrue();
        row.SegmentDurationSeconds.Must().Be(15 * 60);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60 * 60)]
    [InlineData(int.MaxValue)]
    public async Task GetAsyncClampsAnOutOfRangeStoredSegmentDuration(int storedSeconds)
    {
        // Arrange
        await using var db = CreateContext();
        db.AppSettings.Add(new AppSettings { SegmentDurationSeconds = storedSeconds });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var response = await service.GetAsync(CancellationToken.None);

        // Assert
        response.SegmentDurationSeconds.Must().Be(SegmentDurationLimits.ClampSegmentDuration(storedSeconds));
    }

    private static SettingsService CreateService(AppDbContext db)
    {
        var storage = Options.Create(new StorageOptions());
        return new SettingsService(db, storage, new ProcessCpuSampler(), new StorageMetricsCache(storage, TimeProvider.System));
    }

    private static SettingsWriteRequest Write(float sampleFps, float confidence) =>
        new(30, 90, null, true, sampleFps, confidence, SegmentDurationLimits.DefaultSegmentDurationSeconds);
}
