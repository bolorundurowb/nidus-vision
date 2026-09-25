using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;

namespace NidusVision.Web.Settings;

public sealed class SettingsService(AppDbContext db, IOptions<StorageOptions> storage, ProcessCpuSampler cpu, StorageMetricsCache metricsCache)
{
    public async Task<SettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var row = await db.AppSettings.AsNoTracking().OrderBy(s => s.Id).FirstAsync(cancellationToken);
        return ToResponse(row);
    }

    public async Task<SettingsResponse> UpdateAsync(SettingsWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.GeneralRetentionDays, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.DetectionRetentionDays, 1);
        if (request.MaxStorageBytes is { } maxStorageBytes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxStorageBytes, 1);
        }

        var row = await db.AppSettings.OrderBy(s => s.Id).FirstAsync(cancellationToken);
        row.GeneralRetentionDays = request.GeneralRetentionDays;
        row.DetectionRetentionDays = request.DetectionRetentionDays;
        row.MaxStorageBytes = request.MaxStorageBytes;
        row.InferenceEnabled = request.InferenceEnabled;
        row.SampleFps = request.SampleFps;
        row.ConfidenceThreshold = request.ConfidenceThreshold;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(row);
    }

    public StorageMetrics GetStorageMetrics() => metricsCache.Get();

    public SystemMetricsResponse GetSystemMetrics()
    {
        var process = Process.GetCurrentProcess();
        var storageMetrics = GetStorageMetrics();
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informational) ? "1.0.0" : informational.Split('+')[0];
        return new SystemMetricsResponse(
            cpu.Sample(process),
            process.WorkingSet64,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime(),
            null,
            storageMetrics,
            version);
    }

    private SettingsResponse ToResponse(AppSettings row) => new(
        row.GeneralRetentionDays,
        row.DetectionRetentionDays,
        row.MaxStorageBytes,
        row.InferenceEnabled,
        row.SampleFps,
        row.ConfidenceThreshold,
        Path.GetFullPath(storage.Value.RecordingsDirectory));
}
