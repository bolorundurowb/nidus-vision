namespace NidusVision.Inference;

public static class PersonModelPath
{
    public const string FileName = "person.onnx";
    public const string EnvironmentVariable = "NIDUS_PERSON_MODEL";

    public static string? Resolve(string? contentRoot)
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } configured)
        {
            return File.Exists(configured) ? Path.GetFullPath(configured) : null;
        }

        foreach (var candidate in Candidates(contentRoot))
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    public static IEnumerable<string> Candidates(string? contentRoot)
    {
        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            yield return Path.Combine(contentRoot, "models", FileName);
        }

        yield return Path.Combine(AppContext.BaseDirectory, "models", FileName);
        yield return Path.Combine(Environment.CurrentDirectory, "models", FileName);
    }
}
