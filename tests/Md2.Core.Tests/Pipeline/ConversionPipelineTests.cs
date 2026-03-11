// agent-notes: { ctx: "Issue 5 pipeline skeleton tests, TDD red", deps: [Md2.Core.Pipeline, Md2.Core.Transforms, Md2.Core.Emit], state: "red", last: "tara@2026-03-11" }

using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Parsing;
using Shouldly;

namespace Md2.Core.Tests.Pipeline;

public class ConversionPipelineTests
{
    // ── Parse ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SimpleMarkdown_ReturnsMarkdownDocumentWithExpectedStructure()
    {
        // Arrange
        var pipeline = new ConversionPipeline();
        var markdown = "# Hello\n\nA paragraph with **bold** text.";
        var options = new ParserOptions();

        // Act
        var doc = pipeline.Parse(markdown, options);

        // Assert
        doc.ShouldNotBeNull();
        doc.ShouldBeOfType<MarkdownDocument>();
        doc.Count.ShouldBeGreaterThanOrEqualTo(2); // heading + paragraph
    }

    [Fact]
    public void Parse_EmptyString_ReturnsDocument()
    {
        var pipeline = new ConversionPipeline();
        var doc = pipeline.Parse("", new ParserOptions());

        doc.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_NullMarkdown_ThrowsArgumentNullException()
    {
        var pipeline = new ConversionPipeline();

        Should.Throw<ArgumentNullException>(() => pipeline.Parse(null!, new ParserOptions()));
    }

    // ── ParserOptions defaults ─────────────────────────────────────────

    [Fact]
    public void ParserOptions_DefaultValues_AllTrue()
    {
        var options = new ParserOptions();

        options.EnableGfm.ShouldBeTrue();
        options.EnableMath.ShouldBeTrue();
        options.EnableAdmonitions.ShouldBeTrue();
        options.EnableDefinitionLists.ShouldBeTrue();
        options.EnableAttributes.ShouldBeTrue();
        options.EnableYamlFrontMatter.ShouldBeTrue();
    }

    // ── Transform ──────────────────────────────────────────────────────

    [Fact]
    public void Transform_ExecutesTransformsInOrderSequence()
    {
        // Arrange
        var pipeline = new ConversionPipeline();
        var doc = new MarkdownDocument();
        var executionOrder = new List<int>();

        var transform1 = new StubTransform("First", order: 10, onTransform: () => executionOrder.Add(10));
        var transform2 = new StubTransform("Second", order: 20, onTransform: () => executionOrder.Add(20));
        var transform3 = new StubTransform("Third", order: 5, onTransform: () => executionOrder.Add(5));

        pipeline.RegisterTransform(transform1);
        pipeline.RegisterTransform(transform2);
        pipeline.RegisterTransform(transform3);

        var options = new TransformOptions();

        // Act
        pipeline.Transform(doc, options);

        // Assert
        executionOrder.ShouldBe(new[] { 5, 10, 20 });
    }

    [Fact]
    public void Transform_EmptyTransformList_ReturnsDocumentUnchanged()
    {
        var pipeline = new ConversionPipeline();
        var doc = new MarkdownDocument();
        var options = new TransformOptions();

        var result = pipeline.Transform(doc, options);

        result.ShouldBe(doc);
    }

    [Fact]
    public void Transform_NullDocument_ThrowsArgumentNullException()
    {
        var pipeline = new ConversionPipeline();

        Should.Throw<ArgumentNullException>(() => pipeline.Transform(null!, new TransformOptions()));
    }

    // ── TransformOptions defaults ──────────────────────────────────────

    [Fact]
    public void TransformOptions_DefaultValues_AreFalse()
    {
        var options = new TransformOptions();

        options.SmartTypography.ShouldBeFalse();
        options.GenerateToc.ShouldBeFalse();
        options.GenerateCoverPage.ShouldBeFalse();
        options.RenderMermaid.ShouldBeFalse();
    }

    // ── TransformContext ───────────────────────────────────────────────

    [Fact]
    public void TransformContext_ExposesDocumentMetadata()
    {
        var metadata = new DocumentMetadata();
        var options = new TransformOptions();
        var context = new TransformContext(metadata, options);

        context.Metadata.ShouldBe(metadata);
        context.Options.ShouldBe(options);
    }

    [Fact]
    public void TransformContext_WarningsListStartsEmpty()
    {
        var context = new TransformContext(new DocumentMetadata(), new TransformOptions());

        context.Warnings.ShouldNotBeNull();
        context.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void TransformContext_CanAddWarnings()
    {
        var context = new TransformContext(new DocumentMetadata(), new TransformOptions());

        context.AddWarning("Test warning");

        context.Warnings.Count.ShouldBe(1);
        context.Warnings[0].ShouldBe("Test warning");
    }

    // ── Emit ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Emit_CallsEmitterWithCorrectArguments()
    {
        // Arrange
        var pipeline = new ConversionPipeline();
        var doc = new MarkdownDocument();
        var theme = new ResolvedTheme();
        var emitOptions = new EmitOptions();
        var emitter = new SpyEmitter();
        using var output = new MemoryStream();

        // Act
        await pipeline.Emit(doc, theme, emitter, emitOptions, output);

        // Assert
        emitter.WasCalled.ShouldBeTrue();
        emitter.ReceivedDocument.ShouldBe(doc);
        emitter.ReceivedTheme.ShouldBe(theme);
        emitter.ReceivedOptions.ShouldBe(emitOptions);
    }

    [Fact]
    public async Task Emit_NullEmitter_ThrowsArgumentNullException()
    {
        var pipeline = new ConversionPipeline();
        using var output = new MemoryStream();

        await Should.ThrowAsync<ArgumentNullException>(
            () => pipeline.Emit(new MarkdownDocument(), new ResolvedTheme(), null!, new EmitOptions(), output));
    }

    // ── EmitOptions defaults ───────────────────────────────────────────

    [Fact]
    public void EmitOptions_DefaultValues()
    {
        var options = new EmitOptions();

        options.TemplatePath.ShouldBeNull();
        options.IncludeToc.ShouldBeFalse();
        options.IncludeCoverPage.ShouldBeFalse();
        options.PageSize.ShouldBeNull();
        options.Margins.ShouldBeNull();
    }

    // ── End-to-end pipeline wiring ─────────────────────────────────────

    [Fact]
    public async Task Pipeline_ParseTransformEmit_EndToEnd()
    {
        // Arrange
        var pipeline = new ConversionPipeline();
        var markdown = "# Title\n\nBody text.";
        var parserOptions = new ParserOptions();
        var transformOptions = new TransformOptions();
        var emitOptions = new EmitOptions();
        var theme = new ResolvedTheme();
        var emitter = new SpyEmitter();
        using var output = new MemoryStream();

        // Act: full pipeline wiring
        var doc = pipeline.Parse(markdown, parserOptions);
        var transformed = pipeline.Transform(doc, transformOptions);
        await pipeline.Emit(transformed, theme, emitter, emitOptions, output);

        // Assert
        emitter.WasCalled.ShouldBeTrue();
        emitter.ReceivedDocument.ShouldNotBeNull();
        emitter.ReceivedDocument!.Count.ShouldBeGreaterThan(0);
    }

    // ── IFormatEmitter contract ────────────────────────────────────────

    [Fact]
    public void IFormatEmitter_RequiresFormatNameAndExtensions()
    {
        var emitter = new SpyEmitter();

        emitter.FormatName.ShouldNotBeNullOrEmpty();
        emitter.FileExtensions.ShouldNotBeEmpty();
    }

    // ── Test doubles ───────────────────────────────────────────────────

    private class StubTransform : IAstTransform
    {
        private readonly Action _onTransform;

        public StubTransform(string name, int order, Action onTransform)
        {
            Name = name;
            Order = order;
            _onTransform = onTransform;
        }

        public string Name { get; }
        public int Order { get; }

        public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context)
        {
            _onTransform();
            return doc;
        }
    }

    private class SpyEmitter : IFormatEmitter
    {
        public bool WasCalled { get; private set; }
        public MarkdownDocument? ReceivedDocument { get; private set; }
        public ResolvedTheme? ReceivedTheme { get; private set; }
        public EmitOptions? ReceivedOptions { get; private set; }

        public string FormatName => "test";
        public IReadOnlyList<string> FileExtensions => new[] { ".test" };

        public Task EmitAsync(MarkdownDocument doc, ResolvedTheme theme, EmitOptions options, Stream output)
        {
            WasCalled = true;
            ReceivedDocument = doc;
            ReceivedTheme = theme;
            ReceivedOptions = options;
            return Task.CompletedTask;
        }
    }
}
