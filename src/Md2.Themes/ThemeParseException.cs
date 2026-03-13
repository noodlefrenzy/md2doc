// agent-notes: { ctx: "exception for theme YAML parse errors", deps: [Md2.Core.Exceptions.Md2Exception], state: active, last: "sato@2026-03-13" }

using Md2.Core.Exceptions;

namespace Md2.Themes;

/// <summary>
/// Thrown when a theme YAML file cannot be parsed.
/// </summary>
public class ThemeParseException : Md2Exception
{
    public ThemeParseException(string message) : base(message) { }
    public ThemeParseException(string message, Exception innerException) : base(message, innerException) { }
}
