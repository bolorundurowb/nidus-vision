using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NidusVision.Core.Inference;

namespace NidusVision.Inference;

public sealed class HumanDetector : IHumanDetector, IDisposable
{
    public const float NmsIouThreshold = 0.45f;

    private readonly InferenceSession? _session;
    private readonly string? _inputName;
    private readonly Lock _run = new();
    private readonly ILogger<HumanDetector> _logger;

    public HumanDetector(ILogger<HumanDetector> logger, string? contentRoot = null)
    {
        _logger = logger;
        var model = PersonModelPath.Resolve(contentRoot);
        if (model is null)
        {
            logger.LogInformation(
                "Person model not found (set {Env} or place {File} under models/); person detection stays off.",
                PersonModelPath.EnvironmentVariable,
                PersonModelPath.FileName);
            return;
        }

        var renderPresent = OnnxExecutionProviders.LinuxRenderDevicePresent();
        if (OnnxExecutionProviders.Preferred.Contains(OnnxExecutionProviders.OpenVinoGpu) && !renderPresent)
        {
            logger.LogInformation(
                "Intel GPU device node {Device} is not present; skipping OpenVINO GPU and using CPU.",
                OnnxExecutionProviders.LinuxRenderDevicePath);
        }

        var attempts = OnnxExecutionPlanner.Plan(OnnxExecutionProviders.Preferred, renderPresent);
        try
        {
            _session = OnnxExecutionPlanner.Execute(
                attempts,
                attempt => CreateSession(model, attempt),
                (attempt, ex) => logger.LogWarning(
                    ex,
                    "ONNX providers [{Chain}] failed for profile {Profile}; trying fallback.",
                    string.Join(" -> ", attempt.Providers),
                    OnnxExecutionProviders.ActiveProfileName));

            if (_session is null)
            {
                logger.LogWarning("Failed to initialize ONNX Runtime; falling back to no-op detector.");
                return;
            }

            _inputName = _session.InputMetadata.Keys.First();
            var input = _session.InputMetadata[_inputName];
            var outputName = _session.OutputMetadata.Keys.First();
            var output = _session.OutputMetadata[outputName];
            logger.LogInformation(
                "Loaded ONNX person detector from {Path} ({Input} {InputShape} -> {Output} {OutputShape}).",
                model,
                _inputName,
                FormatShape(input.Dimensions),
                outputName,
                FormatShape(output.Dimensions));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize ONNX Runtime; falling back to no-op detector.");
            _session?.Dispose();
            _session = null;
            ExecutionProvider = null;
        }
    }

    public bool IsAvailable => _session is not null;

    public string? ExecutionProvider { get; private set; }

    public IReadOnlyList<BoundingBox> Detect(
        ReadOnlySpan<byte> rgb24,
        int width,
        int height,
        int sourceWidth,
        int sourceHeight,
        float threshold)
    {
        if (_session is null || rgb24.IsEmpty || width <= 0 || height <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return [];
        }

        var session = _session;

        var transform = LetterboxTransform.For(sourceWidth, sourceHeight);
        byte[] letterboxed;
        if (width == transform.InputSize && height == transform.InputSize)
        {
            letterboxed = rgb24.ToArray();
        }
        else if (width == sourceWidth && height == sourceHeight)
        {
            letterboxed = RgbLetterbox.Apply(rgb24, width, height, transform);
        }
        else
        {
            _logger.LogDebug(
                "Skipping frame with unexpected size {Width}x{Height} (source {SourceWidth}x{SourceHeight}).",
                width,
                height,
                sourceWidth,
                sourceHeight);
            return [];
        }

        var packed = RgbLetterbox.PackNchw(letterboxed, transform.InputSize, transform.InputSize);
        var input = new DenseTensor<float>(packed, [1, 3, transform.InputSize, transform.InputSize]);
        float[] data;
        int[] dims;
        lock (_run)
        {
            using var results = session.Run([NamedOnnxValue.CreateFromTensor(_inputName, input)]);
            var output = results[0].AsTensor<float>();
            data = output.ToArray();
            dims = output.Dimensions.ToArray();
        }

        var decoded = YoloV8Decoder.Decode(data, dims, threshold, transform);
        return NonMaxSuppression.Filter(decoded, NmsIouThreshold);
    }

    public void Dispose() => _session?.Dispose();

    private InferenceSession CreateSession(string model, OnnxSessionAttempt attempt)
    {
        var options = new SessionOptions();
        try
        {
            foreach (var provider in attempt.Providers)
            {
                OnnxExecutionProviders.Append(options, provider);
            }

            var session = new InferenceSession(model, options);
            ExecutionProvider = attempt.Providers[0];
            _logger.LogInformation(
                "Selected ONNX execution provider {Provider} (profile {Profile}, chain {Chain}).",
                ExecutionProvider,
                OnnxExecutionProviders.ActiveProfileName,
                string.Join(" -> ", attempt.Providers));
            return session;
        }
        catch
        {
            options.Dispose();
            throw;
        }
    }

    private static string FormatShape(int[] dims) =>
        "[" + string.Join(',', dims.Select(d => d <= 0 ? "dyn" : d.ToString())) + "]";
}
