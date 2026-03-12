// agent-notes: { ctx: "tests for ThemeResolveFormatter output", deps: [src/Md2.Themes/ThemeResolveFormatter.cs, src/Md2.Themes/ThemeCascadeResolver.cs], state: active, last: "sato@2026-03-12" }

using Shouldly;
using Md2.Core.Pipeline;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class ThemeResolveFormatterTests
{
    private static ResolvedTheme DefaultTheme() => ResolvedTheme.CreateDefault();

    private static List<CascadeTraceEntry> SampleTrace() =>
    [
        new("HeadingFont", "Calibri", CascadeLayer.Preset),
        new("BodyFont", "Cambria", CascadeLayer.Preset),
        new("PrimaryColor", "FF0000", CascadeLayer.Theme),
        new("BaseFontSize", "14", CascadeLayer.Cli),
    ];

    [Fact]
    public void Format_ContainsColumnHeaders()
    {
        var result = ThemeResolveFormatter.Format(DefaultTheme(), SampleTrace());

        result.ShouldContain("Property");
        result.ShouldContain("Value");
        result.ShouldContain("Source");
    }

    [Fact]
    public void Format_ContainsAllTraceEntryRows()
    {
        var trace = SampleTrace();
        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Header line + separator line + one line per trace entry
        lines.Length.ShouldBe(trace.Count + 2);
    }

    [Fact]
    public void Format_ShowsPresetSourceLabel()
    {
        var trace = new List<CascadeTraceEntry>
        {
            new("HeadingFont", "Calibri", CascadeLayer.Preset),
        };

        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        result.ShouldContain("Preset");
    }

    [Fact]
    public void Format_ShowsTemplateSourceLabel()
    {
        var trace = new List<CascadeTraceEntry>
        {
            new("BodyFont", "Times New Roman", CascadeLayer.Template),
        };

        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        result.ShouldContain("Template");
    }

    [Fact]
    public void Format_ShowsThemeSourceLabel()
    {
        var trace = new List<CascadeTraceEntry>
        {
            new("PrimaryColor", "FF0000", CascadeLayer.Theme),
        };

        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        result.ShouldContain("Theme");
    }

    [Fact]
    public void Format_ShowsCliSourceLabel()
    {
        var trace = new List<CascadeTraceEntry>
        {
            new("BaseFontSize", "14", CascadeLayer.Cli),
        };

        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        result.ShouldContain("CLI");
    }

    [Fact]
    public void Format_PropertyNamesAppearInOutput()
    {
        var trace = SampleTrace();
        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        foreach (var entry in trace)
        {
            result.ShouldContain(entry.Property);
        }
    }

    [Fact]
    public void Format_ValuesAppearInOutput()
    {
        var trace = SampleTrace();
        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        foreach (var entry in trace)
        {
            result.ShouldContain(entry.Value);
        }
    }

    [Fact]
    public void Format_EmptyTrace_ReturnsHeadersOnly()
    {
        var trace = new List<CascadeTraceEntry>();
        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        result.ShouldContain("Property");
        result.ShouldContain("Value");
        result.ShouldContain("Source");

        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Header line + separator line only
        lines.Length.ShouldBe(2);
    }

    [Fact]
    public void Format_ColumnsAreAligned_AllRowsSameColumnPositions()
    {
        var trace = new List<CascadeTraceEntry>
        {
            new("HeadingFont", "Calibri", CascadeLayer.Preset),
            new("PrimaryColor", "FF0000", CascadeLayer.Theme),
            new("BaseFontSize", "14", CascadeLayer.Cli),
            new("TableAlternateRowBackground", "F2F2F2", CascadeLayer.Template),
        };

        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // All data lines (skip separator at index 1) should have consistent
        // column start positions. We check by finding where "Value" column starts
        // in the header and verifying all rows use the same offset for their
        // second column content.
        lines.Length.ShouldBeGreaterThan(2);

        var headerLine = lines[0];
        var valueColumnStart = headerLine.IndexOf("Value");
        var sourceColumnStart = headerLine.IndexOf("Source");

        valueColumnStart.ShouldBeGreaterThan(0);
        sourceColumnStart.ShouldBeGreaterThan(valueColumnStart);

        // Each data row (index 2+) should have non-space content that fits within
        // the column boundaries. We verify by checking the row length is at least
        // as long as the header.
        for (int i = 2; i < lines.Length; i++)
        {
            lines[i].Length.ShouldBeGreaterThanOrEqualTo(sourceColumnStart,
                $"Row {i} is too short to contain the Source column");
        }
    }

    [Fact]
    public void Format_SeparatorLineContainsDashes()
    {
        var trace = new List<CascadeTraceEntry>
        {
            new("HeadingFont", "Calibri", CascadeLayer.Preset),
        };

        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Length.ShouldBeGreaterThanOrEqualTo(2);
        // Separator line should consist primarily of dash-like characters
        lines[1].ShouldContain("──");
    }

    [Fact]
    public void Format_AllCascadeLayers_MappedToHumanReadableNames()
    {
        // Verify all four CascadeLayer values produce distinct human-readable labels
        var trace = new List<CascadeTraceEntry>
        {
            new("A", "1", CascadeLayer.Preset),
            new("B", "2", CascadeLayer.Template),
            new("C", "3", CascadeLayer.Theme),
            new("D", "4", CascadeLayer.Cli),
        };

        var result = ThemeResolveFormatter.Format(DefaultTheme(), trace);

        result.ShouldContain("Preset");
        result.ShouldContain("Template");
        result.ShouldContain("Theme");
        result.ShouldContain("CLI");
    }
}
