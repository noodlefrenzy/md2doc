// agent-notes: { ctx: "Issue #72 error handling tests", deps: [Md2.Core.Exceptions, Md2.Parsing.FrontMatterParseException], state: "green", last: "sato@2026-03-12" }

using Md2.Parsing;
using Shouldly;

namespace Md2.Core.Tests;

public class ErrorHandlingTests
{
    // ── Md2Exception base type ──────────────────────────────────────────

    [Fact]
    public void Md2Exception_Exists_AndDerivesFromException()
    {
        // Md2Exception should be in Md2.Core.Exceptions namespace.
        // This will fail until the class is created.
        var ex = new Md2.Core.Exceptions.Md2Exception("something went wrong");

        ex.ShouldBeAssignableTo<Exception>();
        ex.Message.ShouldBe("something went wrong");
    }

    [Fact]
    public void Md2Exception_HasUserMessageProperty()
    {
        // UserMessage is the human-friendly message shown to CLI users,
        // separate from the developer-oriented Exception.Message.
        var ex = new Md2.Core.Exceptions.Md2Exception("internal detail", "Something went wrong. Check your input.");

        ex.UserMessage.ShouldBe("Something went wrong. Check your input.");
        ex.Message.ShouldBe("internal detail");
    }

    [Fact]
    public void Md2Exception_UserMessageDefaultsToMessage_WhenNotProvided()
    {
        var ex = new Md2.Core.Exceptions.Md2Exception("some error");

        ex.UserMessage.ShouldBe("some error");
    }

    [Fact]
    public void Md2Exception_SupportsInnerException()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new Md2.Core.Exceptions.Md2Exception("wrapper", inner);

        ex.InnerException.ShouldBe(inner);
    }

    // ── FrontMatterParseException derives from Md2Exception ─────────────

    [Fact]
    public void FrontMatterParseException_DerivesFromMd2Exception()
    {
        // Currently derives directly from Exception; must be changed to Md2Exception.
        var ex = new FrontMatterParseException("bad yaml", lineNumber: 3);

        ex.ShouldBeAssignableTo<Md2.Core.Exceptions.Md2Exception>();
    }

    [Fact]
    public void FrontMatterParseException_RetainsLineNumber()
    {
        // Existing LineNumber property must survive the re-parenting.
        var ex = new FrontMatterParseException("bad yaml", lineNumber: 7);

        ex.LineNumber.ShouldBe(7);
        ex.ShouldBeAssignableTo<Md2.Core.Exceptions.Md2Exception>();
    }

    // ── Md2ConversionException for pipeline/emit errors ─────────────────

    [Fact]
    public void Md2ConversionException_Exists_AndDerivesFromMd2Exception()
    {
        var ex = new Md2.Core.Exceptions.Md2ConversionException("emit failed");

        ex.ShouldBeAssignableTo<Md2.Core.Exceptions.Md2Exception>();
        ex.Message.ShouldBe("emit failed");
    }

    [Fact]
    public void Md2ConversionException_SupportsInnerException()
    {
        var inner = new IOException("disk full");
        var ex = new Md2.Core.Exceptions.Md2ConversionException("emit failed", inner);

        ex.InnerException.ShouldBe(inner);
        ex.ShouldBeAssignableTo<Md2.Core.Exceptions.Md2Exception>();
    }

    [Fact]
    public void Md2ConversionException_HasUserMessage()
    {
        var ex = new Md2.Core.Exceptions.Md2ConversionException(
            "NullRef in visitor at node X",
            "The document could not be converted. Please report this bug.");

        ex.UserMessage.ShouldBe("The document could not be converted. Please report this bug.");
    }
}
