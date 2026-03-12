// agent-notes: { ctx: "formats cascade trace as aligned table for CLI output", deps: [ThemeCascadeResolver.cs], state: active, last: "sato@2026-03-12" }

using System.Text;
using Md2.Core.Pipeline;

namespace Md2.Themes;

/// <summary>
/// Formats a cascade resolution trace as an aligned table for CLI output.
/// </summary>
public static class ThemeResolveFormatter
{
    private const int ColumnGap = 2;

    /// <summary>
    /// Formats the resolved theme and its cascade trace as a human-readable table.
    /// The theme parameter is reserved for future use (e.g. showing computed values
    /// alongside the cascade trace).
    /// </summary>
    public static string Format(ResolvedTheme theme, IReadOnlyList<CascadeTraceEntry> trace)
    {
        _ = theme; // Reserved for future use — all data currently comes from trace
        const string propHeader = "Property";
        const string valHeader = "Value";
        const string srcHeader = "Source";

        // Calculate column widths
        var propWidth = Math.Max(propHeader.Length, trace.Count > 0 ? trace.Max(e => e.Property.Length) : 0);
        var valWidth = Math.Max(valHeader.Length, trace.Count > 0 ? trace.Max(e => e.Value.Length) : 0);
        var srcWidth = Math.Max(srcHeader.Length, trace.Count > 0 ? trace.Max(e => FormatLayer(e.Source).Length) : 0);

        var sb = new StringBuilder();

        // Header
        sb.Append(propHeader.PadRight(propWidth + ColumnGap));
        sb.Append(valHeader.PadRight(valWidth + ColumnGap));
        sb.AppendLine(srcHeader);

        // Separator
        sb.Append(new string('─', propWidth));
        sb.Append(new string(' ', ColumnGap));
        sb.Append(new string('─', valWidth));
        sb.Append(new string(' ', ColumnGap));
        sb.AppendLine(new string('─', srcWidth));

        // Rows
        foreach (var entry in trace)
        {
            sb.Append(entry.Property.PadRight(propWidth + ColumnGap));
            sb.Append(entry.Value.PadRight(valWidth + ColumnGap));
            sb.AppendLine(FormatLayer(entry.Source));
        }

        return sb.ToString();
    }

    private static string FormatLayer(CascadeLayer layer) => layer switch
    {
        CascadeLayer.Preset => "Preset",
        CascadeLayer.Template => "Template",
        CascadeLayer.Theme => "Theme",
        CascadeLayer.Cli => "CLI",
        _ => layer.ToString()
    };
}
