// agent-notes: { ctx: "Visual regression: JSON snapshots of all preset resolved themes", deps: [PresetRegistry, ThemeCascadeResolver, ResolvedTheme], state: active, last: "tara@2026-03-13" }

using System.Text.Json;
using System.Text.Json.Serialization;
using Md2.Core.Pipeline;
using Md2.Themes;
using Shouldly;

namespace Md2.Themes.Tests;

/// <summary>
/// Snapshot tests for all 5 preset themes. Each preset is resolved through the cascade
/// and serialized to deterministic JSON. If a preset's resolved properties change,
/// the corresponding snapshot must be updated intentionally.
/// </summary>
public class PresetSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly string SnapshotDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "Snapshots", "Presets");

    [Theory]
    [InlineData("default")]
    [InlineData("academic")]
    [InlineData("corporate")]
    [InlineData("minimal")]
    [InlineData("technical")]
    [InlineData("editorial")]
    [InlineData("nightowl")]
    [InlineData("hackterm")]
    [InlineData("bubble")]
    [InlineData("rosegarden")]
    public void Preset_MatchesApprovedSnapshot(string presetName)
    {
        var theme = ThemeCascadeResolver.Resolve(new ThemeCascadeInput { PresetName = presetName });
        var actual = JsonSerializer.Serialize(theme, JsonOptions);

        var snapshotPath = Path.Combine(SnapshotDir, $"{presetName}.json");

        if (!File.Exists(snapshotPath))
        {
            // First run: create the snapshot
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            File.WriteAllText(snapshotPath, actual);
            Assert.Fail($"Snapshot created at {snapshotPath}. Re-run to verify.");
        }

        var expected = File.ReadAllText(snapshotPath);
        actual.ShouldBe(expected, $"Preset '{presetName}' resolved theme has changed. " +
            $"If intentional, delete {snapshotPath} and re-run to update.");
    }

    [Fact]
    public void AllPresets_HaveSnapshots()
    {
        var presets = PresetRegistry.ListPresets();
        presets.Count.ShouldBeGreaterThanOrEqualTo(5);

        foreach (var preset in presets)
        {
            var snapshotPath = Path.Combine(SnapshotDir, $"{preset}.json");
            File.Exists(snapshotPath).ShouldBeTrue($"Missing snapshot for preset '{preset}'");
        }
    }

    [Theory]
    [InlineData("default")]
    [InlineData("academic")]
    [InlineData("corporate")]
    [InlineData("minimal")]
    [InlineData("technical")]
    [InlineData("editorial")]
    [InlineData("nightowl")]
    [InlineData("hackterm")]
    [InlineData("bubble")]
    [InlineData("rosegarden")]
    public void Preset_HasDistinctPrimaryColor(string presetName)
    {
        var theme = ThemeCascadeResolver.Resolve(new ThemeCascadeInput { PresetName = presetName });
        theme.PrimaryColor.ShouldNotBeNullOrEmpty();
        theme.PrimaryColor.Length.ShouldBe(6, $"Primary color should be 6 hex chars for '{presetName}'");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("academic")]
    [InlineData("corporate")]
    [InlineData("minimal")]
    [InlineData("technical")]
    [InlineData("editorial")]
    [InlineData("nightowl")]
    [InlineData("hackterm")]
    [InlineData("bubble")]
    [InlineData("rosegarden")]
    public void Preset_HasValidFontSizes(string presetName)
    {
        var theme = ThemeCascadeResolver.Resolve(new ThemeCascadeInput { PresetName = presetName });

        theme.BaseFontSize.ShouldBeGreaterThan(0);
        theme.Heading1Size.ShouldBeGreaterThan(theme.BaseFontSize);
        theme.Heading1Size.ShouldBeGreaterThanOrEqualTo(theme.Heading2Size);
        theme.Heading2Size.ShouldBeGreaterThanOrEqualTo(theme.Heading3Size);
    }

    [Fact]
    public void AllPresets_HaveDistinctPrimaryColors()
    {
        var presets = PresetRegistry.ListPresets();
        var colors = presets
            .Select(p => ThemeCascadeResolver.Resolve(new ThemeCascadeInput { PresetName = p }).PrimaryColor)
            .ToHashSet();

        // At least 3 distinct primary colors across 5 presets (some may share)
        colors.Count.ShouldBeGreaterThanOrEqualTo(3,
            "Presets should have visual variety — at least 3 distinct primary colors");
    }

    [Fact]
    public void AllPresets_ProduceDeterministicOutput()
    {
        foreach (var presetName in PresetRegistry.ListPresets())
        {
            var theme1 = ThemeCascadeResolver.Resolve(new ThemeCascadeInput { PresetName = presetName });
            var theme2 = ThemeCascadeResolver.Resolve(new ThemeCascadeInput { PresetName = presetName });

            var json1 = JsonSerializer.Serialize(theme1, JsonOptions);
            var json2 = JsonSerializer.Serialize(theme2, JsonOptions);

            json1.ShouldBe(json2, $"Preset '{presetName}' should produce deterministic output");
        }
    }
}
