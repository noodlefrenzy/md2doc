// agent-notes: { ctx: "Parse md2-specific extensions from HTML comments", deps: [YamlDotNet], state: active, last: "sato@2026-03-15" }

using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Md2.Slides;

/// <summary>
/// Parsed md2 extension from an HTML comment.
/// Example: <!-- md2: { build: "bullets", layout: "two-column" } -->
/// </summary>
public record Md2Extension
{
    public string? Build { get; init; }
    public string? Layout { get; init; }
    public string? Transition { get; init; }
    public int? TransitionDurationMs { get; init; }
    public Dictionary<string, object>? Extra { get; init; }
}

/// <summary>
/// Parses md2-specific extension comments (<!-- md2: { ... } -->).
/// These are standard HTML comments that MARP tools would ignore.
/// </summary>
public static partial class MarpExtensionParser
{
    [GeneratedRegex(@"<!--\s*md2:\s*(\{.*?\})\s*-->", RegexOptions.Singleline)]
    private static partial Regex Md2CommentRegex();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Try to parse an HTML comment as an md2 extension.
    /// Returns null if the comment is not an md2 extension.
    /// </summary>
    public static Md2Extension? TryParse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var match = Md2CommentRegex().Match(html.Trim());
        if (!match.Success)
            return null;

        var yamlContent = match.Groups[1].Value;

        try
        {
            var dict = YamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            if (dict == null)
                return null;

            var ext = new Md2Extension
            {
                Build = dict.TryGetValue("build", out var build) ? build?.ToString() : null,
                Layout = dict.TryGetValue("layout", out var layout) ? layout?.ToString() : null,
                Transition = dict.TryGetValue("transition", out var transition) ? transition?.ToString() : null,
                TransitionDurationMs = dict.TryGetValue("transitionDurationMs", out var dur) && dur is string durStr && int.TryParse(durStr, out var durVal) ? durVal : null,
            };

            // Collect extra keys not handled above
            var knownKeys = new HashSet<string> { "build", "layout", "transition", "transitionDurationMs" };
            var extra = dict.Where(kv => !knownKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (extra.Count > 0)
                ext = ext with { Extra = extra };

            return ext;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse md2 extension: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check whether an HTML comment is an md2 extension comment.
    /// </summary>
    public static bool IsMd2Extension(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;
        return Md2CommentRegex().IsMatch(html.Trim());
    }
}
