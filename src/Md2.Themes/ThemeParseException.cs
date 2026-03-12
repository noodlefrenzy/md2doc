// agent-notes: { ctx: "exception for theme YAML parse errors", deps: [], state: active, last: "sato@2026-03-12" }

namespace Md2.Themes;

/// <summary>
/// Thrown when a theme YAML file cannot be parsed.
/// </summary>
public class ThemeParseException : Exception
{
    public ThemeParseException(string message) : base(message) { }
    public ThemeParseException(string message, Exception innerException) : base(message, innerException) { }
}
