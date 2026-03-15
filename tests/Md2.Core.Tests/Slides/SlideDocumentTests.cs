// agent-notes: { ctx: "Tests for SlideDocument IR types (ADR-0014)", deps: [Md2.Core.Slides], state: active, last: "tara@2026-03-15" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Slides;
using Shouldly;

namespace Md2.Core.Tests.Slides;

public class SlideDocumentTests
{
    // ── SlideDocument ─────────────────────────────────────────────────

    [Fact]
    public void SlideDocument_DefaultConstruction_HasEmptySlides()
    {
        var doc = new SlideDocument();
        doc.Slides.ShouldNotBeNull();
        doc.Slides.ShouldBeEmpty();
    }

    [Fact]
    public void SlideDocument_DefaultConstruction_HasDefaultMetadata()
    {
        var doc = new SlideDocument();
        doc.Metadata.ShouldNotBeNull();
    }

    [Fact]
    public void SlideDocument_AddSlide_IncreasesCount()
    {
        var doc = new SlideDocument();
        var slide = new Slide(0, new MarkdownDocument());
        doc.AddSlide(slide);
        doc.Slides.Count.ShouldBe(1);
    }

    [Fact]
    public void SlideDocument_AddMultipleSlides_PreservesOrder()
    {
        var doc = new SlideDocument();
        doc.AddSlide(new Slide(0, new MarkdownDocument()));
        doc.AddSlide(new Slide(1, new MarkdownDocument()));
        doc.AddSlide(new Slide(2, new MarkdownDocument()));

        doc.Slides.Count.ShouldBe(3);
        doc.Slides[0].Index.ShouldBe(0);
        doc.Slides[1].Index.ShouldBe(1);
        doc.Slides[2].Index.ShouldBe(2);
    }

    [Fact]
    public void SlideDocument_Theme_IsNullByDefault()
    {
        var doc = new SlideDocument();
        doc.Theme.ShouldBeNull();
    }

    // ── Slide ─────────────────────────────────────────────────────────

    [Fact]
    public void Slide_Construction_SetsIndexAndContent()
    {
        var content = new MarkdownDocument();
        var slide = new Slide(3, content);

        slide.Index.ShouldBe(3);
        slide.Content.ShouldBeSameAs(content);
    }

    [Fact]
    public void Slide_DefaultLayout_IsContent()
    {
        var slide = new Slide(0, new MarkdownDocument());
        slide.Layout.ShouldBe(SlideLayout.Content);
    }

    [Fact]
    public void Slide_SpeakerNotes_NullByDefault()
    {
        var slide = new Slide(0, new MarkdownDocument());
        slide.SpeakerNotes.ShouldBeNull();
    }

    [Fact]
    public void Slide_SpeakerNotes_CanBeSet()
    {
        var slide = new Slide(0, new MarkdownDocument());
        slide.SpeakerNotes = "These are my notes";
        slide.SpeakerNotes.ShouldBe("These are my notes");
    }

    [Fact]
    public void Slide_Directives_NotNullByDefault()
    {
        var slide = new Slide(0, new MarkdownDocument());
        slide.Directives.ShouldNotBeNull();
    }

    [Fact]
    public void Slide_Build_NullByDefault()
    {
        var slide = new Slide(0, new MarkdownDocument());
        slide.Build.ShouldBeNull();
    }

    [Fact]
    public void Slide_Transition_NullByDefault()
    {
        var slide = new Slide(0, new MarkdownDocument());
        slide.Transition.ShouldBeNull();
    }

    // ── SlideLayout ───────────────────────────────────────────────────

    [Fact]
    public void SlideLayout_WellKnownConstants_ExistAndAreDistinct()
    {
        var layouts = new[]
        {
            SlideLayout.Content,
            SlideLayout.Title,
            SlideLayout.TwoColumn,
            SlideLayout.SectionDivider,
            SlideLayout.Blank
        };

        layouts.Select(l => l.Name).Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public void SlideLayout_Content_HasExpectedName()
    {
        SlideLayout.Content.Name.ShouldBe("content");
    }

    [Fact]
    public void SlideLayout_Title_HasExpectedName()
    {
        SlideLayout.Title.Name.ShouldBe("title");
    }

    [Fact]
    public void SlideLayout_CustomLayout_CanBeCreated()
    {
        var custom = new SlideLayout("lead");
        custom.Name.ShouldBe("lead");
    }

    [Fact]
    public void SlideLayout_EqualityByName()
    {
        var a = new SlideLayout("content");
        var b = SlideLayout.Content;
        a.ShouldBe(b);
    }

    // ── SlideDirectives ───────────────────────────────────────────────

    [Fact]
    public void SlideDirectives_AllProperties_NullByDefault()
    {
        var d = new SlideDirectives();
        d.BackgroundColor.ShouldBeNull();
        d.BackgroundImage.ShouldBeNull();
        d.Color.ShouldBeNull();
        d.Class.ShouldBeNull();
        d.Paginate.ShouldBeNull();
        d.Header.ShouldBeNull();
        d.Footer.ShouldBeNull();
    }

    [Fact]
    public void SlideDirectives_CanSetAllProperties()
    {
        var d = new SlideDirectives
        {
            BackgroundColor = "#123456",
            BackgroundImage = "url(bg.jpg)",
            Color = "#ffffff",
            Class = "invert",
            Paginate = true,
            Header = "My Header",
            Footer = "Page %d"
        };

        d.BackgroundColor.ShouldBe("#123456");
        d.BackgroundImage.ShouldBe("url(bg.jpg)");
        d.Color.ShouldBe("#ffffff");
        d.Class.ShouldBe("invert");
        d.Paginate.ShouldBe(true);
        d.Header.ShouldBe("My Header");
        d.Footer.ShouldBe("Page %d");
    }

    // ── PresentationMetadata ──────────────────────────────────────────

    [Fact]
    public void PresentationMetadata_ImplementsIDocumentMetadata()
    {
        var meta = new PresentationMetadata();
        meta.ShouldBeAssignableTo<IDocumentMetadata>();
    }

    [Fact]
    public void PresentationMetadata_SharedProperties_Work()
    {
        var meta = new PresentationMetadata
        {
            Title = "My Deck",
            Author = "Jane",
            Date = "2026-03-15"
        };

        meta.Title.ShouldBe("My Deck");
        meta.Author.ShouldBe("Jane");
        meta.Date.ShouldBe("2026-03-15");
    }

    [Fact]
    public void PresentationMetadata_PresentationSpecific_Properties()
    {
        var meta = new PresentationMetadata
        {
            Theme = "gaia",
            Size = SlideSize.Widescreen16x9
        };

        meta.Theme.ShouldBe("gaia");
        meta.Size.ShouldBe(SlideSize.Widescreen16x9);
    }

    [Fact]
    public void PresentationMetadata_DefaultSize_IsWidescreen()
    {
        var meta = new PresentationMetadata();
        meta.Size.ShouldBe(SlideSize.Widescreen16x9);
    }

    [Fact]
    public void PresentationMetadata_CustomFields_EmptyByDefault()
    {
        var meta = new PresentationMetadata();
        meta.CustomFields.ShouldNotBeNull();
        meta.CustomFields.ShouldBeEmpty();
    }

    // ── IDocumentMetadata ─────────────────────────────────────────────

    [Fact]
    public void DocumentMetadata_ImplementsIDocumentMetadata()
    {
        var meta = new Md2.Core.Ast.DocumentMetadata();
        meta.ShouldBeAssignableTo<IDocumentMetadata>();
    }

    // ── SlideSize ─────────────────────────────────────────────────────

    [Fact]
    public void SlideSize_Widescreen_HasExpectedDimensions()
    {
        SlideSize.Widescreen16x9.Width.ShouldBe(12192000);  // 16:9 standard EMU
        SlideSize.Widescreen16x9.Height.ShouldBe(6858000);
    }

    [Fact]
    public void SlideSize_Standard_HasExpectedDimensions()
    {
        SlideSize.Standard4x3.Width.ShouldBe(9144000);  // 4:3 standard EMU
        SlideSize.Standard4x3.Height.ShouldBe(6858000);
    }

    [Fact]
    public void SlideSize_Widescreen16x10_HasExpectedDimensions()
    {
        SlideSize.Widescreen16x10.Width.ShouldBe(10972800);
        SlideSize.Widescreen16x10.Height.ShouldBe(6858000);
    }

    // ── BuildAnimation ────────────────────────────────────────────────

    [Fact]
    public void BuildAnimation_CanBeCreated()
    {
        var build = new BuildAnimation(BuildAnimationType.Bullets);
        build.Type.ShouldBe(BuildAnimationType.Bullets);
    }

    // ── SlideTransition ───────────────────────────────────────────────

    [Fact]
    public void SlideTransition_CanBeCreated()
    {
        var transition = new SlideTransition("fade");
        transition.Type.ShouldBe("fade");
    }

    [Fact]
    public void SlideTransition_Duration_HasDefault()
    {
        var transition = new SlideTransition("slide");
        transition.DurationMs.ShouldBeNull();
    }
}
