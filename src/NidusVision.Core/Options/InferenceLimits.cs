namespace NidusVision.Core.Options;

public static class InferenceLimits
{
    public const float MinSampleFps = 0.2f;
    public const float MaxSampleFps = 5f;
    public const float MinConfidence = 0f;
    public const float MaxConfidence = 1f;

    public static float ClampSampleFps(float value) => Math.Clamp(value, MinSampleFps, MaxSampleFps);

    public static float ClampConfidence(float value) => Math.Clamp(value, MinConfidence, MaxConfidence);
}
