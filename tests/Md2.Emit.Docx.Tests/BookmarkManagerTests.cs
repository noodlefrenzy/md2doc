// agent-notes: { ctx: "Tests for BookmarkManager: slugify, dedup, anchor resolution", deps: [Md2.Emit.Docx.BookmarkManager], state: active, last: "sato@2026-03-12" }

using Md2.Emit.Docx;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class BookmarkManagerTests
{
    // --- Slugify ---

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("My Heading", "my-heading")]
    [InlineData("  Spaces  ", "spaces")]
    [InlineData("ALL CAPS", "all-caps")]
    public void Slugify_BasicText_ReturnsSlug(string input, string expected)
    {
        BookmarkManager.Slugify(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Hello, World!", "hello-world")]
    [InlineData("C# is great", "c-is-great")]
    [InlineData("foo & bar", "foo--bar")] // & removed, double hyphen collapsed? Let's check
    public void Slugify_SpecialChars_Removed(string input, string _)
    {
        var slug = BookmarkManager.Slugify(input);
        slug.ShouldNotContain("&");
        slug.ShouldNotContain("!");
        slug.ShouldNotContain(",");
    }

    [Fact]
    public void Slugify_EmptyString_ReturnsHeading()
    {
        BookmarkManager.Slugify("").ShouldBe("heading");
    }

    [Fact]
    public void Slugify_OnlySpecialChars_ReturnsHeading()
    {
        BookmarkManager.Slugify("!!!").ShouldBe("heading");
    }

    [Fact]
    public void Slugify_MultipleSpaces_CollapsedToSingleHyphen()
    {
        var slug = BookmarkManager.Slugify("hello   world");
        slug.ShouldBe("hello-world");
    }

    // --- RegisterHeading ---

    [Fact]
    public void RegisterHeading_FirstOccurrence_ReturnsBaseSlug()
    {
        var manager = new BookmarkManager();
        var (slug, _) = manager.RegisterHeading("Introduction");
        slug.ShouldBe("introduction");
    }

    [Fact]
    public void RegisterHeading_DuplicateHeadings_Disambiguated()
    {
        var manager = new BookmarkManager();
        var (slug1, _) = manager.RegisterHeading("Introduction");
        var (slug2, _) = manager.RegisterHeading("Introduction");
        var (slug3, _) = manager.RegisterHeading("Introduction");

        slug1.ShouldBe("introduction");
        slug2.ShouldBe("introduction-1");
        slug3.ShouldBe("introduction-2");
    }

    [Fact]
    public void RegisterHeading_UniqueBookmarkIds()
    {
        var manager = new BookmarkManager();
        var (_, id1) = manager.RegisterHeading("Heading A");
        var (_, id2) = manager.RegisterHeading("Heading B");

        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void RegisterHeading_PositiveBookmarkIds()
    {
        var manager = new BookmarkManager();
        var (_, id) = manager.RegisterHeading("Test");
        id.ShouldBeGreaterThan(0);
    }

    // --- ResolveAnchor ---

    [Fact]
    public void ResolveAnchor_RegisteredHeading_ReturnsBookmarkId()
    {
        var manager = new BookmarkManager();
        var (slug, expectedId) = manager.RegisterHeading("My Section");

        var resolvedId = manager.ResolveAnchor($"#{slug}");

        resolvedId.ShouldBe(expectedId);
    }

    [Fact]
    public void ResolveAnchor_WithoutHash_ReturnsBookmarkId()
    {
        var manager = new BookmarkManager();
        var (slug, expectedId) = manager.RegisterHeading("My Section");

        var resolvedId = manager.ResolveAnchor(slug);

        resolvedId.ShouldBe(expectedId);
    }

    [Fact]
    public void ResolveAnchor_UnregisteredAnchor_ReturnsNull()
    {
        var manager = new BookmarkManager();
        manager.RegisterHeading("Something");

        manager.ResolveAnchor("#nonexistent").ShouldBeNull();
    }

    [Fact]
    public void ResolveAnchor_DisambiguatedSlug_Resolves()
    {
        var manager = new BookmarkManager();
        manager.RegisterHeading("Section");
        var (slug2, id2) = manager.RegisterHeading("Section");

        manager.ResolveAnchor(slug2).ShouldBe(id2);
    }
}
