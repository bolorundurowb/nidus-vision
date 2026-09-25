using NidusVision.Inference;

namespace NidusVision.Tests;

public sealed class OnnxExecutionPlannerTests
{
    [Fact]
    public void WindowsDefaultProfileIsDirectMlThenCpu()
    {
        if (!OperatingSystem.IsWindows() || OnnxExecutionProviders.ActiveProfileName != OnnxExecutionProviders.DirectMlProfile)
        {
            return;
        }

        OnnxExecutionProviders.Preferred[0].Must().Be(OnnxExecutionProviders.DirectMl);
        OnnxExecutionProviders.Preferred[1].Must().Be(OnnxExecutionProviders.Cpu);
    }

    [Fact]
    public void PreferredProvidersMatchActiveProfile()
    {
        var preferred = OnnxExecutionProviders.Preferred;
        preferred[^1].Must().Be(OnnxExecutionProviders.Cpu);

        switch (OnnxExecutionProviders.ActiveProfileName)
        {
            case OnnxExecutionProviders.DirectMlProfile:
                preferred.Must().HaveCount(2);
                preferred[0].Must().Be(OnnxExecutionProviders.DirectMl);
                break;
            case OnnxExecutionProviders.OpenVinoProfile:
                preferred.Must().HaveCount(2);
                preferred[0].Must().Be(OnnxExecutionProviders.OpenVinoGpu);
                break;
            case OnnxExecutionProviders.CudaProfile:
                preferred.Must().HaveCount(2);
                preferred[0].Must().Be(OnnxExecutionProviders.Cuda);
                break;
            default:
                OnnxExecutionProviders.ActiveProfileName.Must().Be(OnnxExecutionProviders.CpuProfile);
                preferred.Must().HaveCount(1);
                break;
        }
    }

    [Fact]
    public void OpenVinoGpuUsesDeviceThenExplicitCpuFallbackWhenRenderNodeExists()
    {
        var attempts = OnnxExecutionPlanner.Plan(
            [OnnxExecutionProviders.OpenVinoGpu, OnnxExecutionProviders.Cpu],
            linuxRenderDevicePresent: true);

        attempts.Must().HaveCount(2);
        attempts[0].Providers.Must().HaveCount(2);
        attempts[0].Providers[0].Must().Be(OnnxExecutionProviders.OpenVinoGpu);
        attempts[0].Providers[1].Must().Be(OnnxExecutionProviders.Cpu);
        attempts[1].Providers.Must().HaveCount(1);
        attempts[1].Providers[0].Must().Be(OnnxExecutionProviders.Cpu);
    }

    [Fact]
    public void OpenVinoGpuIsSkippedWithoutLinuxRenderNode()
    {
        var attempts = OnnxExecutionPlanner.Plan(
            [OnnxExecutionProviders.OpenVinoGpu, OnnxExecutionProviders.Cpu],
            linuxRenderDevicePresent: false);

        attempts.Must().HaveCount(1);
        attempts[0].Providers.Must().HaveCount(1);
        attempts[0].Providers[0].Must().Be(OnnxExecutionProviders.Cpu);
    }

    [Fact]
    public void DirectMlAndCudaKeepGpuFirstThenCpuFallback()
    {
        var directMl = OnnxExecutionPlanner.Plan(
            [OnnxExecutionProviders.DirectMl, OnnxExecutionProviders.Cpu],
            linuxRenderDevicePresent: false);
        directMl.Must().HaveCount(2);
        directMl[0].Providers[0].Must().Be(OnnxExecutionProviders.DirectMl);
        directMl[1].Providers.Must().HaveCount(1);
        directMl[1].Providers[0].Must().Be(OnnxExecutionProviders.Cpu);

        var cuda = OnnxExecutionPlanner.Plan(
            [OnnxExecutionProviders.Cuda, OnnxExecutionProviders.Cpu],
            linuxRenderDevicePresent: false);
        cuda[0].Providers[0].Must().Be(OnnxExecutionProviders.Cuda);
        cuda[1].Providers.Must().HaveCount(1);
        cuda[1].Providers[0].Must().Be(OnnxExecutionProviders.Cpu);
    }

    [Fact]
    public void ExecuteSelectsGpuWhenCreateSucceeds()
    {
        var attempts = OnnxExecutionPlanner.Plan(
            [OnnxExecutionProviders.OpenVinoGpu, OnnxExecutionProviders.Cpu],
            linuxRenderDevicePresent: true);

        var selected = OnnxExecutionPlanner.Execute(attempts, attempt => attempt.Providers[0]);
        selected.Must().Be(OnnxExecutionProviders.OpenVinoGpu);
    }

    [Fact]
    public void ExecuteFallsBackToCpuWhenGpuCreateFails()
    {
        var attempts = OnnxExecutionPlanner.Plan(
            [OnnxExecutionProviders.OpenVinoGpu, OnnxExecutionProviders.Cpu],
            linuxRenderDevicePresent: true);
        var failures = new List<string>();

        var selected = OnnxExecutionPlanner.Execute(
            attempts,
            attempt =>
            {
                if (attempt.Providers.Contains(OnnxExecutionProviders.OpenVinoGpu))
                {
                    throw new InvalidOperationException("simulated missing GPU");
                }

                return attempt.Providers[0];
            },
            (attempt, _) => failures.Add(string.Join(" -> ", attempt.Providers)));

        selected.Must().Be(OnnxExecutionProviders.Cpu);
        failures.Must().HaveCount(1);
        failures[0].Must().Be("OpenVINO GPU -> CPU");
    }

    [Fact]
    public void LinuxRenderProbeUsesInjectedFilesystem()
    {
        if (!OperatingSystem.IsLinux())
        {
            OnnxExecutionProviders.LinuxRenderDevicePresent(_ => false).Must().BeTrue();
            return;
        }

        OnnxExecutionProviders.LinuxRenderDevicePresent(_ => false).Must().BeFalse();
        OnnxExecutionProviders.LinuxRenderDevicePresent(_ => true).Must().BeTrue();
    }
}
