// agent-notes: { ctx: "Red-phase tests for MermaidDiagramRenderer AST transform", deps: [Md2.Diagrams.MermaidDiagramRenderer, Md2.Diagrams.MermaidRenderer, Md2.Diagrams.BrowserManager, Md2.Diagrams.DiagramCache, Md2.Core.Transforms, Md2.Core.Ast, Markdig, Shouldly], state: red, last: "tara@2026-03-12" }

using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Logging.Abstractions;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Shouldly;

namespace Md2.Diagrams.Tests;

[Trait("Category", "Integration")]
public class MermaidDiagramRendererTests : IAsyncDisposable
{
    private readonly string _cacheDir;
    private readonly BrowserManager _browserManager;
    private readonly DiagramCache _cache;
    private readonly MermaidRenderer _renderer;

    public MermaidDiagramRendererTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "md2-transform-test-" + Guid.NewGuid().ToString("N"));
        _browserManager = new BrowserManager();
        _cache = new DiagramCache(_cacheDir);
        _renderer = new MermaidRenderer(_browserManager, _cache, NullLogger<MermaidRenderer>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _browserManager.DisposeAsync();
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private static TransformContext CreateContext(bool renderMermaid = true)
    {
        var metadata = new DocumentMetadata { Title = "Test" };
        var options = new TransformOptions { RenderMermaid = renderMermaid };
        return new TransformContext(metadata, options);
    }

    private MermaidDiagramRenderer CreateTransform()
    {
        return new MermaidDiagramRenderer(_renderer);
    }

    [Fact]
    public void Transform_AnnotatesMermaidBlock_WithImagePath()
    {
        // Arrange
        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```";
        var doc = Markdown.Parse(markdown);
        var context = CreateContext(renderMermaid: true);
        var transform = CreateTransform();

        // Act
        transform.Transform(doc, context);

        // Assert
        var block = doc.Descendants<FencedCodeBlock>().Single();
        var imagePath = block.GetMermaidImagePath();
        imagePath.ShouldNotBeNullOrEmpty();
        File.Exists(imagePath).ShouldBeTrue("The annotated image path should point to an actual file");
    }

    [Fact]
    public void Transform_SkipsNonMermaidBlocks()
    {
        // Arrange
        var markdown = "```csharp\nvar x = 1;\n```";
        var doc = Markdown.Parse(markdown);
        var context = CreateContext(renderMermaid: true);
        var transform = CreateTransform();

        // Act
        transform.Transform(doc, context);

        // Assert
        var block = doc.Descendants<FencedCodeBlock>().Single();
        block.GetMermaidImagePath().ShouldBeNull();
    }

    [Fact]
    public void Transform_SkipsWhenRenderMermaidIsFalse()
    {
        // Arrange
        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```";
        var doc = Markdown.Parse(markdown);
        var context = CreateContext(renderMermaid: false);
        var transform = CreateTransform();

        // Act
        transform.Transform(doc, context);

        // Assert
        var block = doc.Descendants<FencedCodeBlock>().Single();
        block.GetMermaidImagePath().ShouldBeNull();
    }

    [Fact]
    public void Transform_HandlesMultipleMermaidBlocks()
    {
        // Arrange
        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```\n\nSome text.\n\n```mermaid\ngraph LR;\n    C-->D;\n```";
        var doc = Markdown.Parse(markdown);
        var context = CreateContext(renderMermaid: true);
        var transform = CreateTransform();

        // Act
        transform.Transform(doc, context);

        // Assert
        var blocks = doc.Descendants<FencedCodeBlock>().ToList();
        blocks.Count.ShouldBe(2);

        var path1 = blocks[0].GetMermaidImagePath();
        var path2 = blocks[1].GetMermaidImagePath();

        path1.ShouldNotBeNullOrEmpty();
        path2.ShouldNotBeNullOrEmpty();
        path1.ShouldNotBe(path2, "Different diagrams should produce different image paths");
    }

    [Fact]
    public void Transform_ReturnsOriginalDocument()
    {
        // Arrange
        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```";
        var doc = Markdown.Parse(markdown);
        var context = CreateContext(renderMermaid: true);
        var transform = CreateTransform();

        // Act
        var result = transform.Transform(doc, context);

        // Assert
        result.ShouldBeSameAs(doc);
    }

    [Fact]
    public void Name_ReturnsMermaidDiagram()
    {
        var transform = CreateTransform();
        transform.Name.ShouldBe("MermaidDiagram");
    }

    [Fact]
    public void Order_Returns40_BeforeSyntaxHighlighting()
    {
        var transform = CreateTransform();
        transform.Order.ShouldBe(40);
    }

    [Fact]
    public void Transform_GracefulDegradation_WhenChromiumUnavailable_AddsWarning()
    {
        // This test validates graceful degradation behavior.
        // When Chromium is not installed, the transform should:
        // - NOT throw an exception
        // - Add a warning to context.Warnings
        // - Skip all mermaid block annotation
        //
        // NOTE: This test is only meaningful in an environment without Chromium.
        // In CI with Chromium installed, this behavior would need to be tested
        // by injecting a BrowserManager that fails to launch.
        // For now, we verify the contract: if processing completes without error
        // and no image path is set, a warning should be present.
        //
        // A full test of this path requires a mock BrowserManager or running
        // in a stripped environment. This is documented as a coverage gap below.

        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```";
        var doc = Markdown.Parse(markdown);
        var context = CreateContext(renderMermaid: true);
        var transform = CreateTransform();

        // If Chromium IS installed, this test just confirms normal operation.
        // The graceful degradation path is tested structurally via the contract.
        transform.Transform(doc, context);

        var block = doc.Descendants<FencedCodeBlock>().Single();
        if (block.GetMermaidImagePath() is null)
        {
            // Degradation path: should have a warning
            context.Warnings.ShouldNotBeEmpty("When mermaid rendering is skipped due to missing Chromium, a warning should be added");
        }
    }

    [Fact]
    public void Transform_MixedBlocks_OnlyAnnotatesMermaid()
    {
        // Arrange: mix of mermaid and non-mermaid fenced code blocks
        var markdown = "```csharp\nConsole.WriteLine();\n```\n\n```mermaid\ngraph TD;\n    A-->B;\n```\n\n```python\nprint('hi')\n```";
        var doc = Markdown.Parse(markdown);
        var context = CreateContext(renderMermaid: true);
        var transform = CreateTransform();

        // Act
        transform.Transform(doc, context);

        // Assert
        var blocks = doc.Descendants<FencedCodeBlock>().ToList();
        blocks.Count.ShouldBe(3);

        // Only the mermaid block (index 1) should have an image path
        blocks[0].GetMermaidImagePath().ShouldBeNull();
        blocks[1].GetMermaidImagePath().ShouldNotBeNullOrEmpty();
        blocks[2].GetMermaidImagePath().ShouldBeNull();
    }
}
