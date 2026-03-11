// agent-notes: { ctx: "Syntax highlighting token and font style", deps: [], state: "green", last: "sato@2026-03-11" }

namespace Md2.Core.Ast;

public record SyntaxToken(string Text, string? ForegroundColor, SyntaxFontStyle FontStyle);

public enum SyntaxFontStyle
{
    Normal = 0,
    Bold = 1,
    Italic = 2,
    BoldItalic = 3
}
