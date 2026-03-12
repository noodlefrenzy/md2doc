// agent-notes: { ctx: "loads preset YAML from embedded resources", deps: [ThemeParser.cs, ThemeDefinition.cs], state: active, last: "sato@2026-03-12" }

using System.Collections.Concurrent;
using System.Reflection;

namespace Md2.Themes;

/// <summary>
/// Registry for built-in theme presets loaded from embedded YAML resources.
/// Returns fresh ThemeDefinition instances to prevent mutation of cached data.
/// </summary>
public static class PresetRegistry
{
    private const string ResourcePrefix = "Md2.Themes.Presets.";
    private const string ResourceSuffix = ".yaml";

    private static readonly Assembly Assembly = typeof(PresetRegistry).Assembly;
    private static readonly ConcurrentDictionary<string, string> YamlCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns sorted list of available preset names.
    /// </summary>
    public static IReadOnlyList<string> ListPresets()
    {
        return Assembly
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                        n.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..^ResourceSuffix.Length])
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets a preset by name. Returns a fresh ThemeDefinition each call (YAML string is cached).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the preset name is not found.</exception>
    public static ThemeDefinition GetPreset(string name)
    {
        var yaml = YamlCache.GetOrAdd(name, LoadPresetYaml);
        return ThemeParser.Parse(yaml);
    }

    private static string LoadPresetYaml(string name)
    {
        var resourceName = $"{ResourcePrefix}{name.ToLowerInvariant()}{ResourceSuffix}";
        using var stream = Assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            var available = string.Join(", ", ListPresets());
            throw new ArgumentException(
                $"Unknown preset '{name}'. Available presets: {available}",
                nameof(name));
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
