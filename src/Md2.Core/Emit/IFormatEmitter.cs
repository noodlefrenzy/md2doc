// agent-notes: { ctx: "Format-agnostic emitter interface", deps: [Markdig, ResolvedTheme, EmitOptions], state: "green", last: "sato@2026-03-11" }

using Markdig.Syntax;
using Md2.Core.Pipeline;

namespace Md2.Core.Emit;

public interface IFormatEmitter
{
    string FormatName { get; }
    IReadOnlyList<string> FileExtensions { get; }
    Task EmitAsync(MarkdownDocument doc, ResolvedTheme theme, EmitOptions options, Stream output);
}
