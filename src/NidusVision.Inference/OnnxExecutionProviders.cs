using Microsoft.ML.OnnxRuntime;

namespace NidusVision.Inference;

public static class OnnxExecutionProviders
{
    public const string Cpu = "CPU";
    public const string OpenVinoGpu = "OpenVINO GPU";
    public const string DirectMl = "DirectML";
    public const string Cuda = "CUDA";
    public const string LinuxRenderDevicePath = "/dev/dri";

    public const string CpuProfile = "Cpu";
    public const string OpenVinoProfile = "OpenVino";
    public const string DirectMlProfile = "DirectML";
    public const string CudaProfile = "Cuda";

    public static string ActiveProfileName =>
#if ONNX_EP_OPENVINO
        OpenVinoProfile;
#elif ONNX_EP_DIRECTML
        DirectMlProfile;
#elif ONNX_EP_CUDA
        CudaProfile;
#else
        CpuProfile;
#endif

    public static IReadOnlyList<string> Preferred =>
#if ONNX_EP_OPENVINO
        [OpenVinoGpu, Cpu];
#elif ONNX_EP_DIRECTML
        [DirectMl, Cpu];
#elif ONNX_EP_CUDA
        [Cuda, Cpu];
#else
        [Cpu];
#endif

    public static bool LinuxRenderDevicePresent(Func<string, bool>? directoryExists = null)
    {
        if (!OperatingSystem.IsLinux())
        {
            return true;
        }

        var exists = directoryExists ?? Directory.Exists;
        return exists(LinuxRenderDevicePath);
    }

    public static void Append(SessionOptions options, string provider)
    {
        ArgumentNullException.ThrowIfNull(options);
        switch (provider)
        {
            case Cpu:
                options.AppendExecutionProvider_CPU();
                break;
            case OpenVinoGpu:
                AppendOpenVinoGpu(options);
                break;
            case DirectMl:
                AppendDirectMl(options);
                break;
            case Cuda:
                AppendCuda(options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown ONNX execution provider.");
        }
    }

#if ONNX_EP_OPENVINO
    private static void AppendOpenVinoGpu(SessionOptions options) =>
        options.AppendExecutionProvider_OpenVINO("GPU");
#else
    private static void AppendOpenVinoGpu(SessionOptions _) =>
        throw new InvalidOperationException("OpenVINO is not included in this ONNX Runtime build profile.");
#endif

#if ONNX_EP_DIRECTML
    private static void AppendDirectMl(SessionOptions options) =>
        options.AppendExecutionProvider_DML(0);
#else
    private static void AppendDirectMl(SessionOptions _) =>
        throw new InvalidOperationException("DirectML is not included in this ONNX Runtime build profile.");
#endif

#if ONNX_EP_CUDA
    private static void AppendCuda(SessionOptions options) =>
        options.AppendExecutionProvider_CUDA(0);
#else
    private static void AppendCuda(SessionOptions _) =>
        throw new InvalidOperationException("CUDA is not included in this ONNX Runtime build profile.");
#endif
}
