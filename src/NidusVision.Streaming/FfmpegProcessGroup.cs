using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NidusVision.Streaming;

/// <summary>
/// Binds every FFmpeg child to the lifetime of the host. On Windows the children join a job
/// object that the kernel tears down when this process dies, so killing the host (or the
/// `dotnet run` terminal) cannot leave FFmpeg recording into the recordings directory. Every
/// platform also gets a sweep when the runtime shuts down; only SIGKILL escapes that.
/// </summary>
internal static partial class FfmpegProcessGroup
{
    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private static readonly ConcurrentDictionary<Process, byte> Tracked = new();
    private static readonly IntPtr Job = CreateKillOnCloseJob();

    static FfmpegProcessGroup() => AppDomain.CurrentDomain.ProcessExit += (_, _) => KillTracked();

    public static void Track(Process process)
    {
        Tracked[process] = 0;
        process.Exited += (_, _) => Tracked.TryRemove(process, out _);
        Assign(process);
        if (process.HasExited)
        {
            Tracked.TryRemove(process, out _);
        }
    }

    private static void Assign(Process process)
    {
        if (Job == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AssignProcessToJobObject(Job, process.Handle);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            /* the child exited before it could be adopted */
        }
    }

    private static void KillTracked()
    {
        foreach (var process in Tracked.Keys)
        {
            Tracked.TryRemove(process, out _);
            try
            {
                if (!process.HasExited)
                {
                    // No WaitForExit here: shutdown handlers are time boxed and the kernel
                    // finishes the termination whether or not this process is still around.
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
            {
                /* already exited or cannot be killed */
            }
        }
    }

    /// <summary>
    /// The handle is deliberately never closed: the kernel closes it when this process ends,
    /// which is what terminates the job. It is also non-inheritable so FFmpeg cannot keep the
    /// job alive by holding a copy.
    /// </summary>
    private static IntPtr CreateKillOnCloseJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return IntPtr.Zero;
        }

        var job = CreateJobObjectW(IntPtr.Zero, IntPtr.Zero);
        if (job == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var limits = new JobObjectExtendedLimitInformation();
        limits.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, buffer, false);
            if (SetInformationJobObject(job, JobObjectExtendedLimitInformationClass, buffer, (uint)size))
            {
                return job;
            }

            CloseHandle(job);
            return IntPtr.Zero;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr CreateJobObjectW(IntPtr securityAttributes, IntPtr name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}
