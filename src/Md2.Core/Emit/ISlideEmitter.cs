// agent-notes: { ctx: "Emitter interface for slide-based output (PPTX)", deps: [SlideDocument, ResolvedTheme, EmitOptions], state: active, last: "sato@2026-03-15" }

using Md2.Core.Pipeline;
using Md2.Core.Slides;

namespace Md2.Core.Emit;

/// <summary>
/// Emitter interface for slide-based output formats (e.g., PPTX).
/// Parallel to IFormatEmitter (DOCX) but accepts SlideDocument instead of MarkdownDocument.
/// </summary>
public interface ISlideEmitter
{
    string FormatName { get; }
    Task EmitAsync(SlideDocument doc, ResolvedTheme theme, EmitOptions options, Stream output);
}
