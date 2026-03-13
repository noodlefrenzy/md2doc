// agent-notes: { ctx: "TDD tests for ThemeParser YAML deserialization", deps: [src/Md2.Themes/ThemeParser.cs, src/Md2.Themes/ThemeDefinition.cs, src/Md2.Themes/ThemeParseException.cs], state: active, last: "tara@2026-03-13" }

using Md2.Core.Exceptions;
using Shouldly;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class ThemeParserTests
{
    [Fact]
    public void Parse_FullTheme_ReturnsAllSections()
    {
        var yaml = """
            meta:
              name: test-theme
              description: A test theme
              version: 1
            typography:
              headingFont: Arial
              bodyFont: Georgia
              monoFont: "Fira Code"
              monoFontFallback: "Courier New"
            colors:
              primary: "#1B3A5C"
              secondary: "#4A90D9"
              bodyText: "#333333"
              codeBackground: "#F5F5F5"
              codeBorder: "#E0E0E0"
              link: "#4A90D9"
              tableHeaderBackground: "#1B3A5C"
              tableHeaderForeground: "#FFFFFF"
              tableBorder: "#BFBFBF"
              tableAlternateRow: "#F2F2F2"
              blockquoteBorder: "#4A90D9"
              blockquoteText: "#555555"
            docx:
              baseFontSize: 12
              heading1Size: 30
              heading2Size: 24
              heading3Size: 18
              heading4Size: 14
              heading5Size: 12
              heading6Size: 12
              lineSpacing: 1.5
              tableBorderWidth: 6
              blockquoteIndentTwips: 720
              page:
                width: 12240
                height: 15840
                marginTop: 1440
                marginBottom: 1440
                marginLeft: 1800
                marginRight: 1800
            """;

        var theme = ThemeParser.Parse(yaml);

        theme.Meta.ShouldNotBeNull();
        theme.Meta!.Name.ShouldBe("test-theme");
        theme.Meta.Description.ShouldBe("A test theme");
        theme.Meta.Version.ShouldBe(1);

        theme.Typography.ShouldNotBeNull();
        theme.Typography!.HeadingFont.ShouldBe("Arial");
        theme.Typography.BodyFont.ShouldBe("Georgia");
        theme.Typography.MonoFont.ShouldBe("Fira Code");
        theme.Typography.MonoFontFallback.ShouldBe("Courier New");

        theme.Colors.ShouldNotBeNull();
        theme.Colors!.Primary.ShouldBe("1B3A5C");
        theme.Colors.Secondary.ShouldBe("4A90D9");
        theme.Colors.BodyText.ShouldBe("333333");

        theme.Docx.ShouldNotBeNull();
        theme.Docx!.BaseFontSize.ShouldBe(12.0);
        theme.Docx.Heading1Size.ShouldBe(30.0);
        theme.Docx.LineSpacing.ShouldBe(1.5);
        theme.Docx.TableBorderWidth.ShouldBe(6);
        theme.Docx.Page.ShouldNotBeNull();
        theme.Docx.Page!.Width.ShouldBe(12240u);
        theme.Docx.Page.MarginLeft.ShouldBe(1800);
    }

    [Fact]
    public void Parse_PartialTheme_MissingPropertiesAreNull()
    {
        var yaml = """
            meta:
              name: minimal
              version: 1
            typography:
              headingFont: Helvetica
            """;

        var theme = ThemeParser.Parse(yaml);

        theme.Meta!.Name.ShouldBe("minimal");
        theme.Typography!.HeadingFont.ShouldBe("Helvetica");
        theme.Typography.BodyFont.ShouldBeNull();
        theme.Colors.ShouldBeNull();
        theme.Docx.ShouldBeNull();
    }

    [Fact]
    public void Parse_EmptyYaml_ReturnsEmptyDefinition()
    {
        var theme = ThemeParser.Parse("");

        theme.ShouldNotBeNull();
        theme.Meta.ShouldBeNull();
        theme.Typography.ShouldBeNull();
        theme.Colors.ShouldBeNull();
        theme.Docx.ShouldBeNull();
    }

    [Fact]
    public void Parse_UnknownProperties_IgnoredSilently()
    {
        var yaml = """
            meta:
              name: future-theme
              version: 1
              futureField: some-value
            typography:
              headingFont: Arial
              unknownFont: Comic Sans
            someNewSection:
              property: value
            """;

        var theme = ThemeParser.Parse(yaml);

        theme.Meta!.Name.ShouldBe("future-theme");
        theme.Typography!.HeadingFont.ShouldBe("Arial");
    }

    [Fact]
    public void Parse_InvalidYaml_ThrowsThemeParseException()
    {
        var yaml = """
            meta:
              name: bad
              version: [invalid
            """;

        Should.Throw<ThemeParseException>(() => ThemeParser.Parse(yaml));
    }

    [Fact]
    public void Parse_InvalidPropertyType_ThrowsThemeParseException()
    {
        var yaml = """
            meta:
              name: bad
              version: not-a-number
            """;

        Should.Throw<ThemeParseException>(() => ThemeParser.Parse(yaml));
    }

    [Fact]
    public void ParseFile_ReadsFromDisk()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "meta:\n  name: file-test\n  version: 1\n");
            var theme = ThemeParser.ParseFile(path);
            theme.Meta!.Name.ShouldBe("file-test");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseFile_MissingFile_ThrowsFileNotFound()
    {
        Should.Throw<FileNotFoundException>(() => ThemeParser.ParseFile("/nonexistent/theme.yaml"));
    }

    [Fact]
    public void Parse_ColorsWithAndWithoutHash_BothWork()
    {
        var yaml = """
            colors:
              primary: "#FF0000"
              secondary: "00FF00"
            """;

        var theme = ThemeParser.Parse(yaml);

        // Both are normalized to bare hex (# stripped)
        theme.Colors!.Primary.ShouldBe("FF0000");
        theme.Colors.Secondary.ShouldBe("00FF00");
    }

    [Fact]
    public void Parse_DocxPageLayout_AllFieldsPopulated()
    {
        var yaml = """
            docx:
              page:
                width: 11906
                height: 16838
                marginTop: 1440
                marginBottom: 1440
                marginLeft: 1800
                marginRight: 1800
            """;

        var theme = ThemeParser.Parse(yaml);

        theme.Docx!.Page!.Width.ShouldBe(11906u);
        theme.Docx.Page.Height.ShouldBe(16838u);
        theme.Docx.Page.MarginTop.ShouldBe(1440);
        theme.Docx.Page.MarginBottom.ShouldBe(1440);
        theme.Docx.Page.MarginLeft.ShouldBe(1800);
        theme.Docx.Page.MarginRight.ShouldBe(1800);
    }

    [Fact]
    public void Parse_ColorsNormalized_HashStripped()
    {
        var yaml = """
            colors:
              primary: "#AABBCC"
              secondary: "112233"
              bodyText: "#445566"
            """;

        var theme = ThemeParser.Parse(yaml);

        // All colors stored as bare hex, regardless of whether # was in YAML
        theme.Colors!.Primary.ShouldBe("AABBCC");
        theme.Colors.Secondary.ShouldBe("112233");
        theme.Colors.BodyText.ShouldBe("445566");
    }

    [Fact]
    public void Parse_MetaVersionMissing_DefaultsToNull()
    {
        var yaml = """
            meta:
              name: no-version
            """;

        var theme = ThemeParser.Parse(yaml);
        theme.Meta!.Name.ShouldBe("no-version");
        theme.Meta.Version.ShouldBeNull();
    }

    // ── #81: ThemeParseException should extend Md2Exception ──────────

    [Fact]
    public void ThemeParseException_IsMd2Exception()
    {
        var ex = new ThemeParseException("bad yaml");

        ex.ShouldBeAssignableTo<Md2Exception>();
    }

    [Fact]
    public void ThemeParseException_UserMessage_ContainsMessage()
    {
        var ex = new ThemeParseException("Invalid YAML at line 5");

        ex.UserMessage.ShouldBe("Invalid YAML at line 5");
    }

    [Fact]
    public void ThemeParseException_WithInnerException_IsMd2Exception()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ThemeParseException("bad yaml", inner);

        ex.ShouldBeAssignableTo<Md2Exception>();
        ex.InnerException.ShouldBe(inner);
        ex.UserMessage.ShouldBe("bad yaml");
    }
}
