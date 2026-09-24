using System.Diagnostics;
using Microsoft.Extensions.Options;
using NidusVision.Core.Contracts;
using NidusVision.Core.Options;

namespace NidusVision.Web.Settings;

public sealed class ProcessCpuSampler
{
    private readonly object _gate = new();
    private TimeSpan _lastCpu;
    private DateTime _lastUtc;
    private double _percent;

    public double Sample(Process process)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            var cpu = process.TotalProcessorTime;
            if (_lastUtc == default)
            {
                _lastCpu = cpu;
                _lastUtc = now;
                return 0;
            }

            var wallMs = (now - _lastUtc).TotalMilliseconds;
            var deltaMs = (cpu - _lastCpu).TotalMilliseconds;
            _lastCpu = cpu;
            _lastUtc = now;
            if (wallMs <= 0)
            {
                return _percent;
            }

            _percent = Math.Clamp(deltaMs / wallMs / Math.Max(1, Environment.ProcessorCount) * 100, 0, 100);
            return _percent;
        }
    }
}

public sealed class StorageMetricsCache(IOptions<StorageOptions> storage, TimeProvider time)
{
    private readonly object _gate = new();
    private StorageMetrics? _cached;
    private DateTimeOffset _cachedAt;

    public StorageMetrics Get()
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (_cached is { } cached && now - _cachedAt < TimeSpan.FromSeconds(30))
            {
                return cached;
            }

            _cached = Compute();
            _cachedAt = now;
            return _cached;
        }
    }

    private StorageMetrics Compute()
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

    private static long DirSize(string path) =>
        Directory.Exists(path)
            ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
            : 0;
}
