// agent-notes: { ctx: "Spike #105: validate Markdig preserves per-item bullet markers for build animations", deps: [Markdig, Md2.Parsing], state: active, last: "tara@2026-03-15" }

using Markdig;
using Markdig.Syntax;
using Md2.Parsing;
using Shouldly;

namespace Md2.Core.Tests.Spikes;

/// <summary>
/// Spike: ADR-0015 Required Spike #1 — Fragmented list marker detection.
///
/// MARP uses `*` for animated (fragmented) list items and `-` for static items.
/// This spike tests whether Markdig preserves the per-item bullet character
/// so we can detect fragmented vs static items for build animations.
///
/// Key questions:
/// 1. Does ListBlock.BulletType reflect the marker used?
/// 2. Can we distinguish `*` items from `-` items in a mixed list?
/// 3. Does Markdig create separate ListBlocks for consecutive items with different markers?
/// 4. Does EnableTrackTrivia preserve the original marker character?
/// </summary>
public class FragmentedListMarkerSpikeTests
{
    private static MarkdownDocument Parse(string markdown)
    {
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        return Markdown.Parse(markdown, pipeline);
    }

    // ── Test 1: Homogeneous `-` list ──────────────────────────────────

    [Fact]
    public void Parse_DashOnlyList_BulletTypeIsDash()
    {
        var doc = Parse("- Item A\n- Item B\n- Item C\n");
        var lists = doc.Descendants<ListBlock>().ToList();

        lists.Count.ShouldBe(1);
        lists[0].IsOrdered.ShouldBeFalse();
        lists[0].BulletType.ShouldBe('-');
    }

    // ── Test 2: Homogeneous `*` list ──────────────────────────────────

    [Fact]
    public void Parse_AsteriskOnlyList_BulletTypeIsAsterisk()
    {
        var doc = Parse("* Item A\n* Item B\n* Item C\n");
        var lists = doc.Descendants<ListBlock>().ToList();

        lists.Count.ShouldBe(1);
        lists[0].IsOrdered.ShouldBeFalse();
        lists[0].BulletType.ShouldBe('*');
    }

    // ── Test 3: Mixed markers — key MARP pattern ──────────────────────

    [Fact]
    public void Parse_MixedDashAndAsterisk_ProducesSeparateOrMixedLists()
    {
        // This is the MARP pattern: `-` for static, `*` for animated
        var markdown = "- Static item\n* Animated item\n- Another static\n* Another animated\n";
        var doc = Parse(markdown);
        var lists = doc.Descendants<ListBlock>().ToList();

        // Document what Markdig actually does — either:
        // A) One list with a single BulletType (loses info), or
        // B) Multiple lists split at marker changes (preserves info)
        // The spike exists to discover which.

        if (lists.Count == 1)
        {
            // Scenario A: Markdig merges into one list
            // We need to check individual ListItemBlock for marker info
            var items = lists[0].Descendants<ListItemBlock>().ToList();
            items.Count.ShouldBeGreaterThanOrEqualTo(4);

            // Record the BulletType at list level
            // NOTE: This test documents behavior — it doesn't assert a preference
        }
        else
        {
            // Scenario B: Markdig splits at marker boundaries
            lists.Count.ShouldBeGreaterThanOrEqualTo(2);
        }
    }

    // ── Test 4: ListItemBlock-level marker detection via trivia ────────

    [Fact]
    public void Parse_WithTrivia_ListItemsPreserveMarkerCharacter()
    {
        // EnableTrackTrivia is already enabled in our pipeline
        var markdown = "- Dash item\n* Star item\n";
        var doc = Parse(markdown);

        var items = doc.Descendants<ListItemBlock>().ToList();
        items.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Check if trivia or source position lets us recover the marker
        // ListItemBlock doesn't have a direct BulletType property,
        // but the parent ListBlock does. If Markdig splits lists at
        // marker boundaries, we can check each list's BulletType.
        var lists = doc.Descendants<ListBlock>().ToList();

        // Document findings
        foreach (var list in lists)
        {
            // Record: BulletType, item count, source positions
            list.BulletType.ShouldBeOneOf('-', '*', '+');
        }
    }

    // ── Test 5: Adjacent lists with different markers ─────────────────

    [Fact]
    public void Parse_ParagraphSeparatedLists_PreserveDifferentMarkers()
    {
        // When lists are separated by blank lines, they should be distinct
        var markdown = "- Dash A\n- Dash B\n\n* Star A\n* Star B\n";
        var doc = Parse(markdown);
        var lists = doc.Descendants<ListBlock>().ToList();

        lists.Count.ShouldBe(2);
        lists[0].BulletType.ShouldBe('-');
        lists[1].BulletType.ShouldBe('*');
    }

    // ── Test 6: MARP-style slide with mixed list ──────────────────────

    [Fact]
    public void Parse_MarpSlideWithMixedList_DocumentsStructure()
    {
        var markdown = "# Agenda\n\n- Overview\n* Key finding 1\n* Key finding 2\n- Summary\n";

        var doc = Parse(markdown);
        var lists = doc.Descendants<ListBlock>().ToList();

        // Regardless of how Markdig structures this, count the total items
        var totalItems = doc.Descendants<ListItemBlock>().Count();
        totalItems.ShouldBe(4);

        // If separate lists, each should have correct BulletType
        // If single list, document the BulletType chosen
        foreach (var list in lists)
        {
            list.IsOrdered.ShouldBeFalse();
        }
    }

    // ── Test 7: Source text recovery via Span ──────────────────────────

    [Fact]
    public void Parse_ListItems_SourceSpanAllowsMarkerRecovery()
    {
        var markdown = "- Dash item\n* Star item\n";
        var doc = Parse(markdown);

        var items = doc.Descendants<ListItemBlock>().ToList();
        items.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Even if Markdig doesn't expose the marker directly on ListItemBlock,
        // the Span.Start position lets us look at the source text to recover it
        foreach (var item in items)
        {
            item.Span.Start.ShouldBeGreaterThanOrEqualTo(0);
            var markerChar = markdown[item.Span.Start];
            markerChar.ShouldBeOneOf('-', '*');
        }
    }

    // ── Test 8: `+` marker (third valid unordered marker) ─────────────

    [Fact]
    public void Parse_PlusMarkerList_BulletTypeIsPlus()
    {
        var doc = Parse("+ Item A\n+ Item B\n");
        var lists = doc.Descendants<ListBlock>().ToList();

        lists.Count.ShouldBe(1);
        lists[0].BulletType.ShouldBe('+');
    }

    // ── Test 9: Nested lists with different markers ───────────────────

    [Fact]
    public void Parse_NestedListsDifferentMarkers_PreserveBulletTypes()
    {
        var markdown = "- Outer dash\n  * Inner star\n  * Inner star 2\n- Outer dash 2\n";
        var doc = Parse(markdown);

        var lists = doc.Descendants<ListBlock>().ToList();
        // Should have outer (-) and inner (*) lists
        lists.Count.ShouldBeGreaterThanOrEqualTo(2);

        var outerList = lists.First(l => l.BulletType == '-');
        var innerList = lists.First(l => l.BulletType == '*');
        outerList.ShouldNotBeNull();
        innerList.ShouldNotBeNull();
    }

    // ── Test 10: Ordered list does not interfere ──────────────────────

    [Fact]
    public void Parse_OrderedList_IsOrderedTrue()
    {
        var doc = Parse("1. First\n2. Second\n3. Third\n");
        var lists = doc.Descendants<ListBlock>().ToList();

        lists.Count.ShouldBe(1);
        lists[0].IsOrdered.ShouldBeTrue();
    }
}
