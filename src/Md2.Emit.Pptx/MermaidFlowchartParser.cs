// agent-notes: { ctx: "Parse simple Mermaid flowchart syntax into graph structure for native PPTX shapes", deps: [], state: active, last: "sato@2026-03-15" }

using System.Text.RegularExpressions;

namespace Md2.Emit.Pptx;

/// <summary>
/// Parses simple Mermaid flowchart syntax into a graph structure.
/// Supports graph/flowchart direction, node definitions with shapes, and edges with labels.
/// Returns null for non-flowchart diagram types (sequence, gantt, etc.) — those use image fallback.
/// </summary>
public static partial class MermaidFlowchartParser
{
    /// <summary>
    /// Attempts to parse Mermaid source as a flowchart. Returns null if not a flowchart.
    /// </summary>
    public static FlowchartGraph? TryParse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        var lines = source.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        // First line must declare a flowchart/graph
        var firstLine = lines[0].Trim();
        var dirMatch = DirectionRegex().Match(firstLine);
        if (!dirMatch.Success)
            return null;

        var direction = dirMatch.Groups["dir"].Value.ToUpperInvariant() switch
        {
            "TB" or "TD" => FlowDirection.TopToBottom,
            "BT" => FlowDirection.BottomToTop,
            "LR" => FlowDirection.LeftToRight,
            "RL" => FlowDirection.RightToLeft,
            _ => FlowDirection.TopToBottom
        };

        var graph = new FlowchartGraph(direction);

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("%%"))
                continue; // skip comments

            // Try to parse as edge (A --> B, A -->|label| B, etc.)
            var edgeMatch = EdgeRegex().Match(line);
            if (edgeMatch.Success)
            {
                var fromRaw = edgeMatch.Groups["from"].Value.Trim();
                var toRaw = edgeMatch.Groups["to"].Value.Trim();
                var edgeLabel = edgeMatch.Groups["label"].Value.Trim();
                var arrow = edgeMatch.Groups["arrow"].Value;

                var fromNode = ParseNodeReference(fromRaw, graph);
                var toNode = ParseNodeReference(toRaw, graph);

                var edgeStyle = arrow.Contains("-.") || arrow.Contains(".-")
                    ? EdgeStyle.Dashed
                    : arrow.Contains("==")
                        ? EdgeStyle.Thick
                        : EdgeStyle.Solid;

                graph.Edges.Add(new FlowchartEdge(fromNode.Id, toNode.Id, edgeLabel, edgeStyle));
                continue;
            }

            // Try to parse as standalone node definition (A[Label], B{Decision}, etc.)
            var nodeMatch = StandaloneNodeRegex().Match(line);
            if (nodeMatch.Success)
            {
                var id = nodeMatch.Groups["id"].Value.Trim();
                var shape = nodeMatch.Groups["shape"].Value;
                var label = nodeMatch.Groups["label"].Value.Trim();

                if (!graph.Nodes.ContainsKey(id))
                {
                    graph.Nodes[id] = new FlowchartNode(id, label, ParseShapeType(shape, label));
                }
                else
                {
                    graph.Nodes[id].Label = label;
                    graph.Nodes[id].Shape = ParseShapeType(shape, label);
                }
            }
        }

        return graph.Nodes.Count > 0 ? graph : null;
    }

    private static FlowchartNode ParseNodeReference(string raw, FlowchartGraph graph)
    {
        // Could be "A[Label]" or just "A"
        var nodeDefMatch = StandaloneNodeRegex().Match(raw);
        if (nodeDefMatch.Success)
        {
            var id = nodeDefMatch.Groups["id"].Value.Trim();
            var shape = nodeDefMatch.Groups["shape"].Value;
            var label = nodeDefMatch.Groups["label"].Value.Trim();
            if (!graph.Nodes.ContainsKey(id))
                graph.Nodes[id] = new FlowchartNode(id, label, ParseShapeType(shape, label));
            else if (!string.IsNullOrEmpty(label))
            {
                graph.Nodes[id].Label = label;
                graph.Nodes[id].Shape = ParseShapeType(shape, label);
            }
            return graph.Nodes[id];
        }

        // Plain ID reference
        var plainId = raw.Trim();
        if (!graph.Nodes.ContainsKey(plainId))
            graph.Nodes[plainId] = new FlowchartNode(plainId, plainId, NodeShape.Rectangle);
        return graph.Nodes[plainId];
    }

    private static NodeShape ParseShapeType(string shapeMarker, string label)
    {
        if (string.IsNullOrEmpty(shapeMarker)) return NodeShape.Rectangle;

        return shapeMarker[0] switch
        {
            '[' => NodeShape.Rectangle,
            '(' when shapeMarker.StartsWith("((") => NodeShape.Circle,
            '(' => NodeShape.RoundedRectangle,
            '{' when shapeMarker.StartsWith("{{") => NodeShape.Hexagon,
            '{' => NodeShape.Diamond,
            '>' => NodeShape.Asymmetric,
            _ => NodeShape.Rectangle
        };
    }

    // Matches: graph TD, graph LR, flowchart TB, etc.
    [GeneratedRegex(@"^(?:graph|flowchart)\s+(?<dir>TB|TD|BT|LR|RL)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DirectionRegex();

    // Matches edges: A -->|label| B, A --> B, A -.->|label| B, A ==> B
    [GeneratedRegex(@"^(?<from>[^\-=.]+?)(?<arrow>-->|-.->|==>|---)(?:\|(?<label>[^|]*)\|)?\s*(?<to>.+)$")]
    private static partial Regex EdgeRegex();

    // Matches node definitions: A[Label], B{Decision}, C(Rounded), D((Circle)), E{{Hex}}
    [GeneratedRegex(@"^(?<id>[A-Za-z_][A-Za-z0-9_]*)(?<shape>\[{1,2}|\({1,2}|\{{1,2}|>)(?<label>[^\]\)\}]*)[\]\)\}]+\s*$")]
    private static partial Regex StandaloneNodeRegex();
}

public enum FlowDirection
{
    TopToBottom,
    BottomToTop,
    LeftToRight,
    RightToLeft
}

public enum NodeShape
{
    Rectangle,
    RoundedRectangle,
    Diamond,
    Circle,
    Hexagon,
    Asymmetric
}

public enum EdgeStyle
{
    Solid,
    Dashed,
    Thick
}

public class FlowchartGraph
{
    public FlowchartGraph(FlowDirection direction)
    {
        Direction = direction;
    }

    public FlowDirection Direction { get; }
    public Dictionary<string, FlowchartNode> Nodes { get; } = new();
    public List<FlowchartEdge> Edges { get; } = new();
}

public class FlowchartNode
{
    public FlowchartNode(string id, string label, NodeShape shape)
    {
        Id = id;
        Label = label;
        Shape = shape;
    }

    public string Id { get; }
    public string Label { get; set; }
    public NodeShape Shape { get; set; }
}

public record FlowchartEdge(string FromId, string ToId, string? Label, EdgeStyle Style);
