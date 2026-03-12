// agent-notes: { ctx: "Red-phase tests for LaTeX-to-OMML converter via KaTeX+XSLT", deps: [Md2.Math.LatexToOmmlConverter, Md2.Diagrams.BrowserManager, Shouldly], state: red, last: "tara@2026-03-12" }

using Md2.Diagrams;
using Md2.Core.Exceptions;
using Shouldly;

namespace Md2.Math.Tests;

[Trait("Category", "Integration")]
public class LatexToOmmlConverterTests : IAsyncDisposable
{
    private readonly BrowserManager _browserManager;
    private readonly LatexToOmmlConverter _converter;

    public LatexToOmmlConverterTests()
    {
        _browserManager = new BrowserManager();
        _converter = new LatexToOmmlConverter(_browserManager);
    }

    public async ValueTask DisposeAsync()
    {
        await _browserManager.DisposeAsync();
    }

    [Fact]
    public async Task ConvertAsync_SimpleExpression_ReturnsOmml()
    {
        var omml = await _converter.ConvertAsync("x^2 + y^2 = z^2");

        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:oMath");
    }

    [Fact]
    public async Task ConvertAsync_Fraction_ReturnsOmmlWithFraction()
    {
        var omml = await _converter.ConvertAsync(@"\frac{a}{b}");

        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:f>");
    }

    [Fact]
    public async Task ConvertAsync_GreekLetters_ReturnsOmml()
    {
        var omml = await _converter.ConvertAsync(@"\alpha + \beta = \gamma");

        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:oMath");
        // Greek letters should be present as Unicode characters in the OMML
        omml.ShouldContain("\u03B1");
    }

    [Fact]
    public async Task ConvertAsync_Superscript_ReturnsOmmlWithSup()
    {
        var omml = await _converter.ConvertAsync("e^{i\\pi}");

        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:sSup>");
    }

    [Fact]
    public async Task ConvertAsync_Subscript_ReturnsOmmlWithSub()
    {
        var omml = await _converter.ConvertAsync("x_1 + x_2");

        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:sSub>");
    }

    [Fact]
    public async Task ConvertAsync_SquareRoot_ReturnsOmmlWithRad()
    {
        var omml = await _converter.ConvertAsync(@"\sqrt{x}");

        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:rad>");
    }

    [Fact]
    public async Task ConvertAsync_SameExpression_ReturnsSameResult()
    {
        var omml1 = await _converter.ConvertAsync("x^2");
        var omml2 = await _converter.ConvertAsync("x^2");

        omml1.ShouldBe(omml2);
    }

    [Fact]
    public async Task ConvertAsync_NullInput_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _converter.ConvertAsync(null!));
    }

    [Fact]
    public async Task ConvertAsync_EmptyInput_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _converter.ConvertAsync(""));
    }

    [Fact]
    public async Task ConvertAsync_Matrix_ReturnsOmml()
    {
        var omml = await _converter.ConvertAsync(@"\begin{pmatrix} a & b \\ c & d \end{pmatrix}");

        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:oMath");
    }

    [Fact]
    public async Task ConvertBatchAsync_MultipleExpressions_ReturnsAllResults()
    {
        var expressions = new[] { "x^2", @"\frac{1}{2}", @"\alpha" };

        var results = await _converter.ConvertBatchAsync(expressions);

        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => !string.IsNullOrEmpty(r));
        results.ShouldAllBe(r => r.Contains("<m:oMath"));
    }

    [Fact]
    public async Task ConvertBatchAsync_Empty_ReturnsEmpty()
    {
        var results = await _converter.ConvertBatchAsync(Array.Empty<string>());

        results.Count.ShouldBe(0);
    }
}
