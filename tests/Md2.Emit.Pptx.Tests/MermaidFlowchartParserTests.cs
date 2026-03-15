// agent-notes: { ctx: "Tests for MermaidFlowchartParser", deps: [Md2.Emit.Pptx.MermaidFlowchartParser], state: active, last: "tara@2026-03-15" }

using Shouldly;

namespace Md2.Emit.Pptx.Tests;

public class MermaidFlowchartParserTests
{
    // ── Basic parsing ──────────────────────────────────────────────────

    [Fact]
    public void TryParse_SimpleFlowchartTD_ReturnsGraph()
    {
        var source = "graph TD\n    A[Start] --> B[End]";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Direction.ShouldBe(FlowDirection.TopToBottom);
        graph.Nodes.Count.ShouldBe(2);
        graph.Edges.Count.ShouldBe(1);
    }

    [Fact]
    public void TryParse_FlowchartKeyword_Works()
    {
        var source = "flowchart LR\n    A --> B";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Direction.ShouldBe(FlowDirection.LeftToRight);
    }

    [Fact]
    public void TryParse_NonFlowchart_ReturnsNull()
    {
        var source = "sequenceDiagram\n    Alice->>Bob: Hello";
        var graph = MermaidFlowchartParser.TryParse(source);
        graph.ShouldBeNull();
    }

    [Fact]
    public void TryParse_GanttChart_ReturnsNull()
    {
        var source = "gantt\n    title Project\n    section A\n    Task :a1, 2024-01-01, 30d";
        var graph = MermaidFlowchartParser.TryParse(source);
        graph.ShouldBeNull();
    }

    [Fact]
    public void TryParse_EmptyInput_ReturnsNull()
    {
        MermaidFlowchartParser.TryParse("").ShouldBeNull();
        MermaidFlowchartParser.TryParse(null!).ShouldBeNull();
    }

    // ── Node shapes ────────────────────────────────────────────────────

    [Fact]
    public void TryParse_RectangleNode_CorrectShape()
    {
        var source = "graph TD\n    A[Rectangle]";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Nodes["A"].Shape.ShouldBe(NodeShape.Rectangle);
        graph.Nodes["A"].Label.ShouldBe("Rectangle");
    }

    [Fact]
    public void TryParse_RoundedNode_CorrectShape()
    {
        var source = "graph TD\n    A(Rounded)";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Nodes["A"].Shape.ShouldBe(NodeShape.RoundedRectangle);
    }

    [Fact]
    public void TryParse_DiamondNode_CorrectShape()
    {
        var source = "graph TD\n    A{Decision}";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Nodes["A"].Shape.ShouldBe(NodeShape.Diamond);
    }

    [Fact]
    public void TryParse_CircleNode_CorrectShape()
    {
        var source = "graph TD\n    A((Circle))";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Nodes["A"].Shape.ShouldBe(NodeShape.Circle);
    }

    // ── Edges ──────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_LabeledEdge_ParsesLabel()
    {
        var source = "graph TD\n    A -->|Yes| B";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Edges.Count.ShouldBe(1);
        graph.Edges[0].Label.ShouldBe("Yes");
        graph.Edges[0].Style.ShouldBe(EdgeStyle.Solid);
    }

    [Fact]
    public void TryParse_DashedEdge_ParsesStyle()
    {
        var source = "graph TD\n    A -.-> B";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Edges[0].Style.ShouldBe(EdgeStyle.Dashed);
    }

    [Fact]
    public void TryParse_ThickEdge_ParsesStyle()
    {
        var source = "graph TD\n    A ==> B";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Edges[0].Style.ShouldBe(EdgeStyle.Thick);
    }

    // ── Multi-node graph ───────────────────────────────────────────────

    [Fact]
    public void TryParse_ComplexGraph_AllNodesAndEdges()
    {
        var source = """
            graph TD
                A[Start] --> B{Decision}
                B -->|Yes| C[Action]
                B -->|No| D[End]
            """;
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Nodes.Count.ShouldBe(4);
        graph.Edges.Count.ShouldBe(3);
        graph.Nodes["B"].Shape.ShouldBe(NodeShape.Diamond);
    }

    // ── Direction variants ─────────────────────────────────────────────

    [Theory]
    [InlineData("graph TB", FlowDirection.TopToBottom)]
    [InlineData("graph TD", FlowDirection.TopToBottom)]
    [InlineData("graph BT", FlowDirection.BottomToTop)]
    [InlineData("graph LR", FlowDirection.LeftToRight)]
    [InlineData("graph RL", FlowDirection.RightToLeft)]
    public void TryParse_AllDirections(string firstLine, FlowDirection expected)
    {
        var source = $"{firstLine}\n    A --> B";
        var graph = MermaidFlowchartParser.TryParse(source);
        graph.ShouldNotBeNull();
        graph.Direction.ShouldBe(expected);
    }

    // ── Comments ───────────────────────────────────────────────────────

    [Fact]
    public void TryParse_SkipsComments()
    {
        var source = "graph TD\n    %% This is a comment\n    A --> B";
        var graph = MermaidFlowchartParser.TryParse(source);

        graph.ShouldNotBeNull();
        graph.Nodes.Count.ShouldBe(2);
    }
}
