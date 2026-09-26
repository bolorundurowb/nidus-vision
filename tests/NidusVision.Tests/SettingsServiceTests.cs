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

    private static SettingsService CreateService(AppDbContext db)
    {
        var storage = Options.Create(new StorageOptions());
        return new SettingsService(db, storage, new ProcessCpuSampler(), new StorageMetricsCache(storage, TimeProvider.System));
    }

    private static SettingsWriteRequest Write(float sampleFps, float confidence) =>
        new(30, 90, null, true, sampleFps, confidence);
}
