namespace NidusVision.Core.Storage;

public static class StorageRoot
{
    /// <summary>True when <paramref name="path"/> resolves to a location strictly inside <paramref name="root"/>.</summary>
    public static bool Contains(string root, string? path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.GetFullPath(path, fullRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return fullPath.Length > fullRoot.Length + 1
            && fullPath.StartsWith(fullRoot, comparison)
            && (fullPath[fullRoot.Length] == Path.DirectorySeparatorChar
                || fullPath[fullRoot.Length] == Path.AltDirectorySeparatorChar);
    }
}
