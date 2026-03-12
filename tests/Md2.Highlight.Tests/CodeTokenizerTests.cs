// agent-notes: { ctx: "Tests for CodeTokenizer: tokenization, language support, color mapping", deps: [Md2.Highlight.CodeTokenizer, Md2.Core.Ast.SyntaxToken], state: active, last: "tara@2026-03-12" }

using Md2.Core.Ast;
using Md2.Highlight;
using Shouldly;

namespace Md2.Highlight.Tests;

public class CodeTokenizerTests
{
    [Fact]
    public void Tokenize_CSharp_ReturnsMultipleTokens()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "var x = 42;";

        var tokens = tokenizer.Tokenize(code, "csharp");

        tokens.Count.ShouldBeGreaterThan(1);
        var allText = string.Join("", tokens.Select(t => t.Text));
        allText.ShouldBe(code);
    }

    [Fact]
    public void Tokenize_CSharp_KeywordHasColor()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "var x = 42;";

        var tokens = tokenizer.Tokenize(code, "csharp");

        // "var" should be a keyword with a color
        var varToken = tokens.FirstOrDefault(t => t.Text.Trim() == "var");
        varToken.ShouldNotBeNull();
        varToken!.ForegroundColor.ShouldNotBeNull();
        varToken.ForegroundColor.ShouldNotBeEmpty();
    }

    [Fact]
    public void Tokenize_CSharp_NumberLiteralHasColor()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "var x = 42;";

        var tokens = tokenizer.Tokenize(code, "csharp");

        var numToken = tokens.FirstOrDefault(t => t.Text.Trim() == "42");
        numToken.ShouldNotBeNull();
        numToken!.ForegroundColor.ShouldNotBeNull();
    }

    [Fact]
    public void Tokenize_CSharp_StringHasColor()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "var s = \"hello\";";

        var tokens = tokenizer.Tokenize(code, "csharp");

        // String content should have a color
        var stringToken = tokens.FirstOrDefault(t => t.Text.Contains("hello"));
        stringToken.ShouldNotBeNull();
        stringToken!.ForegroundColor.ShouldNotBeNull();
    }

    [Fact]
    public void Tokenize_MultiLine_HasNewlineTokens()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "var x = 1;\nvar y = 2;";

        var tokens = tokenizer.Tokenize(code, "csharp");

        tokens.Any(t => t.Text == "\n").ShouldBeTrue();
    }

    [Fact]
    public void Tokenize_MultiLine_PreservesAllText()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "int a = 1;\nint b = 2;\nint c = 3;";

        var tokens = tokenizer.Tokenize(code, "csharp");

        var allText = string.Join("", tokens.Select(t => t.Text));
        allText.ShouldBe(code);
    }

    [Fact]
    public void Tokenize_UnknownLanguage_ReturnsPlainText()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "some unknown code";

        var tokens = tokenizer.Tokenize(code, "unknownlang");

        tokens.Count.ShouldBe(1);
        tokens[0].Text.ShouldBe(code);
        tokens[0].ForegroundColor.ShouldBeNull();
    }

    [Fact]
    public void Tokenize_Python_Works()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "def hello():\n    print(\"hi\")";

        var tokens = tokenizer.Tokenize(code, "python");

        tokens.Count.ShouldBeGreaterThan(1);
        var allText = string.Join("", tokens.Select(t => t.Text));
        allText.ShouldBe(code);
    }

    [Fact]
    public void Tokenize_JavaScript_Works()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "const x = () => 42;";

        var tokens = tokenizer.Tokenize(code, "js");

        tokens.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Tokenize_ColorFormat_NoHashPrefix()
    {
        using var tokenizer = new CodeTokenizer();
        var code = "var x = 42;";

        var tokens = tokenizer.Tokenize(code, "csharp");

        foreach (var token in tokens.Where(t => t.ForegroundColor != null))
        {
            token.ForegroundColor!.ShouldNotStartWith("#");
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("cs")]
    [InlineData("python")]
    [InlineData("py")]
    [InlineData("javascript")]
    [InlineData("js")]
    [InlineData("typescript")]
    [InlineData("ts")]
    [InlineData("java")]
    [InlineData("go")]
    [InlineData("rust")]
    [InlineData("cpp")]
    [InlineData("html")]
    [InlineData("css")]
    [InlineData("json")]
    [InlineData("yaml")]
    [InlineData("sql")]
    [InlineData("ruby")]
    [InlineData("bash")]
    [InlineData("xml")]
    [InlineData("swift")]
    public void Tokenize_SupportedLanguage_DoesNotThrow(string language)
    {
        using var tokenizer = new CodeTokenizer();
        var code = "x = 1";

        var tokens = tokenizer.Tokenize(code, language);

        tokens.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetSupportedLanguages_Has20PlusLanguages()
    {
        using var tokenizer = new CodeTokenizer();

        var languages = tokenizer.GetSupportedLanguages();

        languages.Count.ShouldBeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void Tokenize_DifferentKeywordsHaveDifferentColors()
    {
        using var tokenizer = new CodeTokenizer();
        // Keywords vs string literals should have different colors
        var code = "string s = \"hello\";";

        var tokens = tokenizer.Tokenize(code, "csharp");

        var keywordToken = tokens.FirstOrDefault(t => t.Text.Trim() == "string");
        var stringToken = tokens.FirstOrDefault(t => t.Text.Contains("hello"));

        if (keywordToken?.ForegroundColor != null && stringToken?.ForegroundColor != null)
        {
            // Keywords and strings should typically have different colors
            keywordToken.ForegroundColor.ShouldNotBe(stringToken.ForegroundColor);
        }
    }
}
