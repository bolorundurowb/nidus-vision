namespace NidusVision.Inference;

public sealed record OnnxSessionAttempt(IReadOnlyList<string> Providers);

public static class OnnxExecutionPlanner
{
    public static IReadOnlyList<OnnxSessionAttempt> Plan(
        IReadOnlyList<string> preferred,
        bool linuxRenderDevicePresent)
    {
        ArgumentNullException.ThrowIfNull(preferred);
        if (preferred.Count == 0)
        {
            throw new ArgumentException("At least one execution provider is required.", nameof(preferred));
        }

        var gpu = preferred
            .Where(provider => provider != OnnxExecutionProviders.Cpu)
            .Where(provider => linuxRenderDevicePresent || provider != OnnxExecutionProviders.OpenVinoGpu)
            .ToList();

        var attempts = new List<OnnxSessionAttempt>();
        var primary = new List<string>(gpu) { OnnxExecutionProviders.Cpu };
        attempts.Add(new OnnxSessionAttempt(primary));

        if (primary.Count > 1)
        {
            attempts.Add(new OnnxSessionAttempt([OnnxExecutionProviders.Cpu]));
        }

        return attempts;
    }

    public static TResult? Execute<TResult>(
        IReadOnlyList<OnnxSessionAttempt> attempts,
        Func<OnnxSessionAttempt, TResult> create,
        Action<OnnxSessionAttempt, Exception>? onFailure = null)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(create);

        foreach (var attempt in attempts)
        {
            try
            {
                return create(attempt);
            }
            catch (Exception ex)
            {
                onFailure?.Invoke(attempt, ex);
            }
        }

        return null;
    }
}
