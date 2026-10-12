namespace Orderly.Infrastructure;

public static class OrdersFileLocation
{
    public const string DefaultRelativePath = "data/orders.json";

    public static string Resolve(string contentRoot, string? configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var relative = string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultRelativePath
            : configuredPath.Trim();

        var root = Path.GetFullPath(contentRoot);
        var full = Path.GetFullPath(Path.IsPathRooted(relative) ? relative : Path.Combine(root, relative));
        if (!IsUnderRoot(root, full))
            throw new InvalidOperationException("Storage:OrdersFile must be located under the content root.");

        return full;
    }

    private static bool IsUnderRoot(string root, string full)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, comparison);
    }
}
