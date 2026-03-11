// agent-notes: { ctx: "Options controlling AST transform phase", deps: [], state: "green", last: "sato@2026-03-11" }

namespace Md2.Core.Pipeline;

public class TransformOptions
{
    public bool SmartTypography { get; set; }
    public bool GenerateToc { get; set; }
    public bool GenerateCoverPage { get; set; }
    public bool RenderMermaid { get; set; }
}
