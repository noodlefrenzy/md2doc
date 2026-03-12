// agent-notes: { ctx: "Tokenizes code using TextMateSharp grammars into SyntaxTokens", deps: [TextMateSharp, TextMateSharp.Grammars, Md2.Core.Ast.SyntaxToken], state: active, last: "sato@2026-03-12" }

using Md2.Core.Ast;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Md2.Highlight;

public sealed class CodeTokenizer : IDisposable
{
    private readonly RegistryOptions _registryOptions;
    private readonly Registry _registry;

    public CodeTokenizer()
    {
        _registryOptions = new RegistryOptions(ThemeName.LightPlus);
        _registry = new Registry(_registryOptions);
    }

    /// <summary>
    /// Tokenizes the given code string using the TextMate grammar for the specified language.
    /// Returns a flat list of SyntaxTokens preserving line breaks as newline tokens.
    /// </summary>
    public IReadOnlyList<SyntaxToken> Tokenize(string code, string language)
    {
        var scopeName = MapLanguageToScope(language);
        if (scopeName == null)
        {
            return new[] { new SyntaxToken(code, null, SyntaxFontStyle.Normal) };
        }

        var grammar = _registry.LoadGrammar(scopeName);
        if (grammar == null)
        {
            return new[] { new SyntaxToken(code, null, SyntaxFontStyle.Normal) };
        }

        var theme = _registry.GetTheme();
        var tokens = new List<SyntaxToken>();
        var lines = code.Split('\n');
        IStateStack? ruleStack = null;

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            var result = grammar.TokenizeLine(line, ruleStack, TimeSpan.FromSeconds(5));
            ruleStack = result.RuleStack;

            foreach (var tmToken in result.Tokens)
            {
                var startIndex = tmToken.StartIndex;
                var endIndex = tmToken.EndIndex;

                if (startIndex >= line.Length || startIndex >= endIndex)
                    continue;

                // Clamp endIndex to line length
                if (endIndex > line.Length)
                    endIndex = line.Length;

                var text = line[startIndex..endIndex];
                if (string.IsNullOrEmpty(text))
                    continue;

                var (foreground, fontStyle) = ResolveTokenStyle(tmToken.Scopes, theme);
                tokens.Add(new SyntaxToken(text, foreground, fontStyle));
            }

            if (lineIdx < lines.Length - 1)
            {
                tokens.Add(new SyntaxToken("\n", null, SyntaxFontStyle.Normal));
            }
        }

        return tokens;
    }

    public IReadOnlyList<string> GetSupportedLanguages()
    {
        return _languageMap.Keys.ToList();
    }

    private (string? foreground, SyntaxFontStyle fontStyle) ResolveTokenStyle(
        List<string> scopes, Theme theme)
    {
        string? foreground = null;
        var fontStyle = SyntaxFontStyle.Normal;

        var rules = theme.Match(scopes);

        if (rules != null)
        {
            foreach (var rule in rules)
            {
                if (foreground == null && rule.foreground > 0)
                {
                    foreground = theme.GetColor(rule.foreground);
                }

                if (rule.fontStyle > 0)
                {
                    fontStyle = (SyntaxFontStyle)(int)rule.fontStyle;
                }

                if (foreground != null)
                    break;
            }
        }

        if (foreground != null && foreground.StartsWith('#'))
        {
            foreground = foreground[1..];
        }

        return (foreground, fontStyle);
    }

    private string? MapLanguageToScope(string language)
    {
        var normalized = language.ToLowerInvariant().Trim();
        return _languageMap.TryGetValue(normalized, out var scope) ? scope : null;
    }

    private static readonly Dictionary<string, string> _languageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = "source.c",
        ["cpp"] = "source.cpp",
        ["c++"] = "source.cpp",
        ["csharp"] = "source.cs",
        ["cs"] = "source.cs",
        ["c#"] = "source.cs",
        ["css"] = "source.css",
        ["go"] = "source.go",
        ["golang"] = "source.go",
        ["html"] = "text.html.basic",
        ["java"] = "source.java",
        ["javascript"] = "source.js",
        ["js"] = "source.js",
        ["json"] = "source.json",
        ["markdown"] = "text.html.markdown",
        ["md"] = "text.html.markdown",
        ["php"] = "source.php",
        ["python"] = "source.python",
        ["py"] = "source.python",
        ["ruby"] = "source.ruby",
        ["rb"] = "source.ruby",
        ["rust"] = "source.rust",
        ["rs"] = "source.rust",
        ["shell"] = "source.shell",
        ["bash"] = "source.shell",
        ["sh"] = "source.shell",
        ["sql"] = "source.sql",
        ["swift"] = "source.swift",
        ["typescript"] = "source.ts",
        ["ts"] = "source.ts",
        ["xml"] = "text.xml",
        ["yaml"] = "source.yaml",
        ["yml"] = "source.yaml",
    };

    public void Dispose()
    {
    }
}
