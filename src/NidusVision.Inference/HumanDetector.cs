using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using NidusVision.Core.Inference;

namespace NidusVision.Inference;

public sealed class HumanDetector : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly ILogger<HumanDetector> _logger;

    public HumanDetector(ILogger<HumanDetector> logger)
    {
        _logger = logger;
        var model = Environment.GetEnvironmentVariable("NIDUS_PERSON_MODEL") ?? "models/person.onnx";
        if (!File.Exists(model))
        {
            logger.LogWarning("Person model not found at {Path}; inference disabled until a model is provided.", model);
            return;
        }

        var options = new SessionOptions();
        try
        {
            options.AppendExecutionProvider_CPU();
            _session = new InferenceSession(model, options);
            logger.LogInformation("Loaded ONNX person detector from {Path}.", model);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize ONNX Runtime; falling back to no-op detector.");
        }
    }

    public IReadOnlyList<BoundingBox> Detect(ReadOnlySpan<byte> frame, int width, int height, float threshold)
    {
        if (_session is null || frame.IsEmpty || width <= 0 || height <= 0)
        {
            return [];
        }

        // A real tensor conversion belongs here; without a shipped model we return no detections.
        _ = frame.Length;
        _ = threshold;
        return [];
    }

    public void Dispose() => _session?.Dispose();
}
