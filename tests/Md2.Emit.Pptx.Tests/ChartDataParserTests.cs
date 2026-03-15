// agent-notes: { ctx: "Tests for ChartDataParser YAML and CSV formats", deps: [Md2.Emit.Pptx.ChartDataParser], state: active, last: "tara@2026-03-15" }

using Shouldly;

namespace Md2.Emit.Pptx.Tests;

public class ChartDataParserTests
{
    // ── YAML format ────────────────────────────────────────────────────

    [Fact]
    public void TryParse_YamlBarChart_Parses()
    {
        var source = """
            type: bar
            title: Sales
            labels: [Q1, Q2, Q3, Q4]
            series:
            - name: Revenue
              values: [10, 20, 30, 40]
            """;

        var data = ChartDataParser.TryParse(source);

        data.ShouldNotBeNull();
        data.Type.ShouldBe(ChartType.Bar);
        data.Title.ShouldBe("Sales");
        data.Labels.Count.ShouldBe(4);
        data.Series.Count.ShouldBe(1);
        data.Series[0].Name.ShouldBe("Revenue");
        data.Series[0].Values.Count.ShouldBe(4);
    }

    [Fact]
    public void TryParse_YamlLineChart_MultipleSeries()
    {
        var source = """
            type: line
            title: Trends
            labels: [Jan, Feb, Mar]
            series:
            - name: Revenue
              values: [10, 20, 30]
            - name: Costs
              values: [5, 10, 15]
            """;

        var data = ChartDataParser.TryParse(source);

        data.ShouldNotBeNull();
        data.Type.ShouldBe(ChartType.Line);
        data.Series.Count.ShouldBe(2);
    }

    [Fact]
    public void TryParse_YamlPieChart_Parses()
    {
        var source = """
            type: pie
            title: Market Share
            labels: [Chrome, Firefox, Safari]
            series:
            - name: Share
              values: [65, 20, 15]
            """;

        var data = ChartDataParser.TryParse(source);

        data.ShouldNotBeNull();
        data.Type.ShouldBe(ChartType.Pie);
    }

    [Fact]
    public void TryParse_YamlColumnChart_Parses()
    {
        var source = """
            type: column
            title: Results
            labels: [A, B]
            series:
            - name: Score
              values: [80, 90]
            """;

        var data = ChartDataParser.TryParse(source);

        data.ShouldNotBeNull();
        data.Type.ShouldBe(ChartType.Column);
    }

    // ── CSV format ─────────────────────────────────────────────────────

    [Fact]
    public void TryParse_CsvFormat_Parses()
    {
        var source = """
            type: bar
            title: CSV Data
            ---
            Category,Revenue,Costs
            Q1,10,5
            Q2,20,10
            Q3,30,15
            """;

        var data = ChartDataParser.TryParse(source);

        data.ShouldNotBeNull();
        data.Type.ShouldBe(ChartType.Bar);
        data.Title.ShouldBe("CSV Data");
        data.Labels.Count.ShouldBe(3);
        data.Labels[0].ShouldBe("Q1");
        data.Series.Count.ShouldBe(2);
        data.Series[0].Name.ShouldBe("Revenue");
        data.Series[1].Name.ShouldBe("Costs");
    }

    // ── Edge cases ─────────────────────────────────────────────────────

    [Fact]
    public void TryParse_Empty_ReturnsNull()
    {
        ChartDataParser.TryParse("").ShouldBeNull();
        ChartDataParser.TryParse(null!).ShouldBeNull();
    }

    [Fact]
    public void TryParse_InvalidFormat_ReturnsNull()
    {
        ChartDataParser.TryParse("just some random text").ShouldBeNull();
    }

    [Fact]
    public void TryParse_NoSeries_ReturnsNull()
    {
        var source = """
            type: bar
            title: Empty
            labels: [A, B, C]
            """;

        ChartDataParser.TryParse(source).ShouldBeNull();
    }

    [Fact]
    public void TryParse_DefaultsToBar()
    {
        var source = """
            labels: [A, B]
            series:
            - name: Data
              values: [1, 2]
            """;

        var data = ChartDataParser.TryParse(source);
        data.ShouldNotBeNull();
        data.Type.ShouldBe(ChartType.Bar);
    }
}
