// agent-notes: { ctx: "Parse chart data from YAML/CSV code fence for native PPTX charts", deps: [YamlDotNet], state: active, last: "sato@2026-03-15" }

namespace Md2.Emit.Pptx;

/// <summary>
/// Parses chart data from a ```chart code fence.
/// Supports a simple YAML format:
///
///   type: bar
///   title: Sales by Quarter
///   labels: [Q1, Q2, Q3, Q4]
///   series:
///     - name: Revenue
///       values: [10, 20, 30, 40]
///     - name: Costs
///       values: [5, 10, 15, 20]
///
/// Also supports CSV format (auto-detected):
///   type: line
///   title: Monthly Data
///   ---
///   Month,Revenue,Costs
///   Jan,10,5
///   Feb,20,10
/// </summary>
public static class ChartDataParser
{
    public static ChartData? TryParse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        try
        {
            var lines = source.Split('\n');

            // Check for CSV mode (has --- separator)
            var separatorIndex = Array.FindIndex(lines, l => l.Trim() == "---");
            if (separatorIndex >= 0)
                return ParseCsvFormat(lines, separatorIndex);

            return ParseYamlFormat(lines);
        }
        catch
        {
            return null;
        }
    }

    private static ChartData? ParseYamlFormat(string[] lines)
    {
        var chartType = ChartType.Bar;
        var title = "";
        var labels = new List<string>();
        var series = new List<ChartSeries>();
        string? currentSeriesName = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            if (line.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                var typeStr = line["type:".Length..].Trim().ToLowerInvariant();
                chartType = typeStr switch
                {
                    "bar" => ChartType.Bar,
                    "column" => ChartType.Column,
                    "line" => ChartType.Line,
                    "pie" => ChartType.Pie,
                    _ => ChartType.Bar
                };
            }
            else if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                title = line["title:".Length..].Trim();
            }
            else if (line.StartsWith("labels:", StringComparison.OrdinalIgnoreCase))
            {
                labels = ParseInlineArray(line["labels:".Length..].Trim());
            }
            else if (line.StartsWith("- name:", StringComparison.OrdinalIgnoreCase))
            {
                currentSeriesName = line["- name:".Length..].Trim();
            }
            else if (line.StartsWith("values:", StringComparison.OrdinalIgnoreCase) && currentSeriesName != null)
            {
                var values = ParseInlineNumberArray(line["values:".Length..].Trim());
                series.Add(new ChartSeries(currentSeriesName, values));
                currentSeriesName = null;
            }
        }

        if (labels.Count == 0 || series.Count == 0)
            return null;

        return new ChartData(chartType, title, labels, series);
    }

    private static ChartData? ParseCsvFormat(string[] lines, int separatorIndex)
    {
        var chartType = ChartType.Bar;
        var title = "";

        // Parse header section (before ---)
        for (int i = 0; i < separatorIndex; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                var typeStr = line["type:".Length..].Trim().ToLowerInvariant();
                chartType = typeStr switch
                {
                    "bar" => ChartType.Bar,
                    "column" => ChartType.Column,
                    "line" => ChartType.Line,
                    "pie" => ChartType.Pie,
                    _ => ChartType.Bar
                };
            }
            else if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                title = line["title:".Length..].Trim();
            }
        }

        // Parse CSV section (after ---)
        var csvLines = lines.Skip(separatorIndex + 1)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        if (csvLines.Count < 2) return null; // need header + at least 1 data row

        var headerCells = csvLines[0].Split(',').Select(c => c.Trim()).ToList();
        if (headerCells.Count < 2) return null;

        var labels = new List<string>();
        var seriesData = new List<List<double>>();
        for (int s = 1; s < headerCells.Count; s++)
            seriesData.Add(new List<double>());

        for (int r = 1; r < csvLines.Count; r++)
        {
            var cells = csvLines[r].Split(',').Select(c => c.Trim()).ToList();
            if (cells.Count < 2) continue;
            labels.Add(cells[0]);
            for (int s = 1; s < headerCells.Count && s < cells.Count; s++)
            {
                seriesData[s - 1].Add(double.TryParse(cells[s], out var v) ? v : 0);
            }
        }

        var series = new List<ChartSeries>();
        for (int s = 1; s < headerCells.Count; s++)
            series.Add(new ChartSeries(headerCells[s], seriesData[s - 1]));

        if (labels.Count == 0 || series.Count == 0) return null;

        return new ChartData(chartType, title, labels, series);
    }

    private static List<string> ParseInlineArray(string value)
    {
        // Parse [a, b, c] or a, b, c
        var cleaned = value.Trim('[', ']').Trim();
        return cleaned.Split(',').Select(s => s.Trim().Trim('"', '\'')).ToList();
    }

    private static List<double> ParseInlineNumberArray(string value)
    {
        var cleaned = value.Trim('[', ']').Trim();
        return cleaned.Split(',')
            .Select(s => double.TryParse(s.Trim(), out var v) ? v : 0)
            .ToList();
    }
}

public enum ChartType
{
    Bar,
    Column,
    Line,
    Pie
}

public record ChartSeries(string Name, IReadOnlyList<double> Values);

public record ChartData(
    ChartType Type,
    string Title,
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartSeries> Series);
