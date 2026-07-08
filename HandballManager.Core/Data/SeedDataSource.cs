using System.Reflection;

namespace HandballManager.Data;

/// <summary>
/// Reads the seed JSON files embedded in this assembly. Logical resource names are the
/// project-relative paths with forward slashes (pinned in the csproj), e.g.
/// "Data/InitialData/NBI/gyor.json" or "Data/Past Champions/Liga Florilor/champions.json",
/// so the same lookup works on every platform the assembly ships to.
/// </summary>
public static class SeedDataSource
{
    private static readonly Assembly Assembly = typeof(SeedDataSource).Assembly;

    /// <summary>All initial team files as (logical path, JSON content) pairs.</summary>
    public static IReadOnlyList<(string Path, string Json)> GetInitialTeamFiles()
        => Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Data/InitialData/", StringComparison.OrdinalIgnoreCase)
                        && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => (n, ReadText(n)!))
            .ToList();

    /// <summary>Returns the resource's text, or null when no such resource exists.</summary>
    public static string? ReadText(string logicalPath)
    {
        using var stream = Assembly.GetManifestResourceStream(logicalPath);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
