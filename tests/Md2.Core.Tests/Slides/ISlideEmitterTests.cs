// agent-notes: { ctx: "Tests for ISlideEmitter interface contract", deps: [Md2.Core.Slides, Md2.Core.Emit], state: active, last: "tara@2026-03-15" }

using Markdig.Syntax;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Slides;
using Shouldly;

namespace Md2.Core.Tests.Slides;

public class ISlideEmitterTests
{
    private class StubSlideEmitter : ISlideEmitter
    {
        public string FormatName => "pptx";
        public bool WasCalled { get; private set; }
        public SlideDocument? ReceivedDoc { get; private set; }

        public Task EmitAsync(SlideDocument doc, ResolvedTheme theme, EmitOptions options, Stream output, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ReceivedDoc = doc;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void ISlideEmitter_HasFormatName()
    {
        var emitter = new StubSlideEmitter();
        emitter.FormatName.ShouldBe("pptx");
    }

    [Fact]
    public async Task ISlideEmitter_EmitAsync_ReceivesSlideDocument()
    {
        var emitter = new StubSlideEmitter();
        var doc = new SlideDocument();
        doc.AddSlide(new Slide(0, new MarkdownDocument()));

        await emitter.EmitAsync(doc, new ResolvedTheme(), new EmitOptions(), Stream.Null);

        emitter.WasCalled.ShouldBeTrue();
        emitter.ReceivedDoc.ShouldBeSameAs(doc);
    }
}
