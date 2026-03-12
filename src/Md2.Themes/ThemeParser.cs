// agent-notes: { ctx: "YAML theme parser with strict deserialization", deps: [ThemeDefinition.cs], state: active, last: "sato@2026-03-12" }

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Md2.Themes;

/// <summary>
/// Parses YAML theme files into <see cref="ThemeDefinition"/> instances.
/// Unknown properties are silently ignored for forward compatibility.
/// </summary>
public static class ThemeParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses a YAML string into a ThemeDefinition.
    /// </summary>
    /// <exception cref="ThemeParseException">Thrown when the YAML is malformed or contains invalid property types.</exception>
    public static ThemeDefinition Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return new ThemeDefinition();

        try
        {
            return Deserializer.Deserialize<ThemeDefinition>(yaml) ?? new ThemeDefinition();
        }
        catch (YamlException ex)
        {
            throw new ThemeParseException(
                $"Invalid theme YAML at line {ex.Start.Line}, column {ex.Start.Column}: {ex.InnerException?.Message ?? ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Parses a YAML theme file from disk.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="ThemeParseException">Thrown when the YAML is malformed.</exception>
    public static ThemeDefinition ParseFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Theme file not found: {path}", path);

        var yaml = File.ReadAllText(path);
        return Parse(yaml);
    }
}
