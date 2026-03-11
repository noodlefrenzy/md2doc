// agent-notes: { ctx: "Parser config flags for Markdig pipeline", deps: [], state: "green", last: "sato@2026-03-11" }

namespace Md2.Parsing;

public class ParserOptions
{
    public bool EnableGfm { get; set; } = true;
    public bool EnableMath { get; set; } = true;
    public bool EnableAdmonitions { get; set; } = true;
    public bool EnableDefinitionLists { get; set; } = true;
    public bool EnableAttributes { get; set; } = true;
    public bool EnableYamlFrontMatter { get; set; } = true;
}
