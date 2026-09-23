using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Contracts;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;

namespace NidusVision.Web.Settings;

public sealed class SettingsService(AppDbContext db, IOptions<StorageOptions> storage)
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

    public StorageMetrics GetStorageMetrics()
    {
        var recordings = Path.GetFullPath(storage.Value.RecordingsDirectory);
        Directory.CreateDirectory(recordings);
        long generalBytes = DirSize(recordings);
        var events = Path.GetFullPath(storage.Value.EventsDirectory);
        Directory.CreateDirectory(events);
        long detectionBytes = DirSize(events);
        var dataDir = Path.GetFullPath(storage.Value.DataDirectory);
        Directory.CreateDirectory(dataDir);
        long dbBytes = DirSize(dataDir);
        var root = Path.GetPathRoot(recordings);
        long total = 0;
        if (root is not null)
        {
            var drive = new DriveInfo(root);
            total = drive.IsReady ? drive.TotalSize : 0;
        }

        return new StorageMetrics(
            generalBytes + detectionBytes + dbBytes,
            total,
            generalBytes,
            detectionBytes,
            dbBytes);
    }

    public SystemMetricsResponse GetSystemMetrics()
    {
        var process = Process.GetCurrentProcess();
        var storageMetrics = GetStorageMetrics();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.8.2";
        var cpu = Environment.ProcessorCount == 0
            ? 0
            : process.TotalProcessorTime.TotalMilliseconds / Math.Max(1, (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalMilliseconds) / Environment.ProcessorCount * 100;
        return new SystemMetricsResponse(
            Math.Clamp(cpu, 0, 100),
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

    private static long DirSize(string path) =>
        Directory.Exists(path)
            ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
            : 0;
}
