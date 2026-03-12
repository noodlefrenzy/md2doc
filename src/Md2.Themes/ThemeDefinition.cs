// agent-notes: { ctx: "YAML theme model — all nullable for partial themes", deps: [], state: active, last: "sato@2026-03-12" }

using YamlDotNet.Serialization;

namespace Md2.Themes;

/// <summary>
/// Root model for a theme YAML file. All sections are nullable to support partial themes.
/// </summary>
public class ThemeDefinition
{
    [YamlMember(Alias = "meta")]
    public ThemeMetaSection? Meta { get; set; }

    [YamlMember(Alias = "typography")]
    public ThemeTypographySection? Typography { get; set; }

    [YamlMember(Alias = "colors")]
    public ThemeColorsSection? Colors { get; set; }

    [YamlMember(Alias = "docx")]
    public ThemeDocxSection? Docx { get; set; }
}

public class ThemeMetaSection
{
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "version")]
    public int? Version { get; set; }
}

public class ThemeTypographySection
{
    [YamlMember(Alias = "headingFont")]
    public string? HeadingFont { get; set; }

    [YamlMember(Alias = "bodyFont")]
    public string? BodyFont { get; set; }

    [YamlMember(Alias = "monoFont")]
    public string? MonoFont { get; set; }

    [YamlMember(Alias = "monoFontFallback")]
    public string? MonoFontFallback { get; set; }
}

public class ThemeColorsSection
{
    private string? _primary;
    private string? _secondary;
    private string? _bodyText;
    private string? _codeBackground;
    private string? _codeBorder;
    private string? _link;
    private string? _tableHeaderBackground;
    private string? _tableHeaderForeground;
    private string? _tableBorder;
    private string? _tableAlternateRow;
    private string? _blockquoteBorder;
    private string? _blockquoteText;

    [YamlMember(Alias = "primary")]
    public string? Primary { get => _primary; set => _primary = NormalizeHex(value); }

    [YamlMember(Alias = "secondary")]
    public string? Secondary { get => _secondary; set => _secondary = NormalizeHex(value); }

    [YamlMember(Alias = "bodyText")]
    public string? BodyText { get => _bodyText; set => _bodyText = NormalizeHex(value); }

    [YamlMember(Alias = "codeBackground")]
    public string? CodeBackground { get => _codeBackground; set => _codeBackground = NormalizeHex(value); }

    [YamlMember(Alias = "codeBorder")]
    public string? CodeBorder { get => _codeBorder; set => _codeBorder = NormalizeHex(value); }

    [YamlMember(Alias = "link")]
    public string? Link { get => _link; set => _link = NormalizeHex(value); }

    [YamlMember(Alias = "tableHeaderBackground")]
    public string? TableHeaderBackground { get => _tableHeaderBackground; set => _tableHeaderBackground = NormalizeHex(value); }

    [YamlMember(Alias = "tableHeaderForeground")]
    public string? TableHeaderForeground { get => _tableHeaderForeground; set => _tableHeaderForeground = NormalizeHex(value); }

    [YamlMember(Alias = "tableBorder")]
    public string? TableBorder { get => _tableBorder; set => _tableBorder = NormalizeHex(value); }

    [YamlMember(Alias = "tableAlternateRow")]
    public string? TableAlternateRow { get => _tableAlternateRow; set => _tableAlternateRow = NormalizeHex(value); }

    [YamlMember(Alias = "blockquoteBorder")]
    public string? BlockquoteBorder { get => _blockquoteBorder; set => _blockquoteBorder = NormalizeHex(value); }

    [YamlMember(Alias = "blockquoteText")]
    public string? BlockquoteText { get => _blockquoteText; set => _blockquoteText = NormalizeHex(value); }

    /// <summary>
    /// Strips leading '#' from hex color values for compatibility with ResolvedTheme (bare hex).
    /// </summary>
    internal static string? NormalizeHex(string? value) =>
        value?.TrimStart('#');
}

public class ThemeDocxSection
{
    [YamlMember(Alias = "baseFontSize")]
    public double? BaseFontSize { get; set; }

    [YamlMember(Alias = "heading1Size")]
    public double? Heading1Size { get; set; }

    [YamlMember(Alias = "heading2Size")]
    public double? Heading2Size { get; set; }

    [YamlMember(Alias = "heading3Size")]
    public double? Heading3Size { get; set; }

    [YamlMember(Alias = "heading4Size")]
    public double? Heading4Size { get; set; }

    [YamlMember(Alias = "heading5Size")]
    public double? Heading5Size { get; set; }

    [YamlMember(Alias = "heading6Size")]
    public double? Heading6Size { get; set; }

    [YamlMember(Alias = "lineSpacing")]
    public double? LineSpacing { get; set; }

    [YamlMember(Alias = "tableBorderWidth")]
    public int? TableBorderWidth { get; set; }

    [YamlMember(Alias = "blockquoteIndentTwips")]
    public int? BlockquoteIndentTwips { get; set; }

    [YamlMember(Alias = "page")]
    public ThemePageSection? Page { get; set; }
}

public class ThemePageSection
{
    [YamlMember(Alias = "width")]
    public uint? Width { get; set; }

    [YamlMember(Alias = "height")]
    public uint? Height { get; set; }

    [YamlMember(Alias = "marginTop")]
    public int? MarginTop { get; set; }

    [YamlMember(Alias = "marginBottom")]
    public int? MarginBottom { get; set; }

    [YamlMember(Alias = "marginLeft")]
    public int? MarginLeft { get; set; }

    [YamlMember(Alias = "marginRight")]
    public int? MarginRight { get; set; }
}
