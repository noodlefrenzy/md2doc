// agent-notes: { ctx: "Options controlling document emission", deps: [], state: "green", last: "sato@2026-03-11" }

namespace Md2.Core.Pipeline;

public class EmitOptions
{
    public string? TemplatePath { get; set; }
    public bool IncludeToc { get; set; }
    public bool IncludeCoverPage { get; set; }
    public string? PageSize { get; set; }
    public string? Margins { get; set; }
}
