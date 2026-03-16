// agent-notes: { ctx: "Builds OpenXml list paragraphs from Markdig ListBlock", deps: [ParagraphBuilder, InlineVisitorDelegate, DocumentFormat.OpenXml, Markdig], state: active, last: "sato@2026-03-16" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Md2.Emit.Docx;

public sealed class ListBuilder
{
    private readonly ParagraphBuilder _paragraphBuilder;
    private readonly MainDocumentPart _mainDocumentPart;
    private readonly InlineVisitorDelegate? _inlineVisitor;
    private int _nextAbstractNumId = 1;
    private int _nextNumId = 1;

    private static readonly string[] BulletChars = { "\u2022", "\u25E6", "\u25AA" }; // •, ◦, ▪

    public ListBuilder(ParagraphBuilder paragraphBuilder, MainDocumentPart mainDocumentPart, InlineVisitorDelegate? inlineVisitor = null)
    {
        _paragraphBuilder = paragraphBuilder;
        _mainDocumentPart = mainDocumentPart;
        _inlineVisitor = inlineVisitor;
    }

    public List<Paragraph> Build(ListBlock listBlock, int nestLevel = 0)
    {
        var paragraphs = new List<Paragraph>();
        bool isOrdered = listBlock.IsOrdered;
        bool isTight = !listBlock.IsLoose;

        int numId = EnsureNumberingDefinition(isOrdered);

        foreach (var item in listBlock)
        {
            if (item is ListItemBlock listItem)
            {
                var itemParagraphs = BuildListItem(listItem, numId, nestLevel, isOrdered, isTight);
                paragraphs.AddRange(itemParagraphs);
            }
        }

        return paragraphs;
    }

    private List<Paragraph> BuildListItem(ListItemBlock listItem, int numId, int nestLevel, bool isOrdered, bool isTight)
    {
        var paragraphs = new List<Paragraph>();

        // Check for task list
        bool? isTaskChecked = GetTaskListChecked(listItem);

        foreach (var block in listItem)
        {
            if (block is ParagraphBlock paragraphBlock)
            {
                var paragraph = BuildListParagraph(paragraphBlock, numId, nestLevel, isTaskChecked, isTight);
                paragraphs.Add(paragraph);
                // Only apply task checkbox to first paragraph
                isTaskChecked = null;
            }
            else if (block is ListBlock nestedList)
            {
                var nestedParagraphs = Build(nestedList, nestLevel + 1);
                paragraphs.AddRange(nestedParagraphs);
            }
        }

        return paragraphs;
    }

    private Paragraph BuildListParagraph(ParagraphBlock paragraphBlock, int numId, int nestLevel, bool? isTaskChecked, bool isTight)
    {
        var paragraph = new Paragraph();

        var paragraphProperties = new ParagraphProperties(
            new ParagraphStyleId { Val = "ListParagraph" },
            new NumberingProperties(
                new NumberingLevelReference { Val = nestLevel },
                new NumberingId { Val = numId }
            ),
            new Indentation
            {
                Left = ((nestLevel + 1) * 720).ToString(),
                Hanging = "360"
            }
        );

        if (isTight)
        {
            paragraphProperties.Append(new SpacingBetweenLines { Before = "0", After = "0" });
        }

        paragraph.Append(paragraphProperties);

        // Add task checkbox prefix if applicable
        if (isTaskChecked.HasValue)
        {
            string checkbox = isTaskChecked.Value ? "\u2611 " : "\u2610 ";
            var checkboxRun = _paragraphBuilder.CreateRun(checkbox);
            paragraph.Append(checkboxRun);
        }

        // Add inline content (skip TaskList inlines)
        if (paragraphBlock.Inline != null)
        {
            foreach (var inline in paragraphBlock.Inline)
            {
                if (inline is TaskList)
                    continue;

                if (_inlineVisitor != null)
                {
                    var elements = _inlineVisitor(inline, false, false, false)
                        .Select(e => e.CloneNode(true))
                        .ToList();
                    foreach (var element in elements)
                        paragraph.Append(element);
                }
                else
                {
                    var run = BuildRunFromInline(inline);
                    if (run != null)
                        paragraph.Append(run);
                }
            }
        }

        return paragraph;
    }

    private Run? BuildRunFromInline(Markdig.Syntax.Inlines.Inline inline)
    {
        var text = ExtractInlineText(inline);
        if (string.IsNullOrEmpty(text))
            return null;

        return _paragraphBuilder.CreateRun(text);
    }

    private static string ExtractInlineText(Markdig.Syntax.Inlines.Inline inline) =>
        InlineTextExtractor.Extract(inline);

    private static bool? GetTaskListChecked(ListItemBlock listItem)
    {
        foreach (var block in listItem)
        {
            if (block is ParagraphBlock paragraphBlock && paragraphBlock.Inline != null)
            {
                foreach (var inline in paragraphBlock.Inline)
                {
                    if (inline is TaskList taskList)
                    {
                        return taskList.Checked;
                    }
                }
            }
        }
        return null;
    }

    private int EnsureNumberingDefinition(bool isOrdered)
    {
        var numberingPart = _mainDocumentPart.NumberingDefinitionsPart;
        if (numberingPart == null)
        {
            numberingPart = _mainDocumentPart.AddNewPart<NumberingDefinitionsPart>();
            numberingPart.Numbering = new Numbering();
        }

        var numbering = numberingPart.Numbering;
        int abstractNumId = _nextAbstractNumId++;
        int numId = _nextNumId++;

        var abstractNum = new AbstractNum { AbstractNumberId = abstractNumId };

        // Create levels for nesting (up to 9 levels)
        for (int level = 0; level < 9; level++)
        {
            var levelDef = new Level { LevelIndex = level };

            if (isOrdered)
            {
                levelDef.Append(new NumberingFormat { Val = NumberFormatValues.Decimal });
                levelDef.Append(new LevelText { Val = $"%{level + 1}." });
            }
            else
            {
                levelDef.Append(new NumberingFormat { Val = NumberFormatValues.Bullet });
                levelDef.Append(new LevelText { Val = BulletChars[level % BulletChars.Length] });
            }

            levelDef.Append(new StartNumberingValue { Val = 1 });

            abstractNum.Append(levelDef);
        }

        // Insert abstract numbering before any NumberingInstance elements
        var firstInstance = numbering.Elements<NumberingInstance>().FirstOrDefault();
        if (firstInstance != null)
        {
            numbering.InsertBefore(abstractNum, firstInstance);
        }
        else
        {
            numbering.Append(abstractNum);
        }

        numbering.Append(new NumberingInstance(
            new AbstractNumId { Val = abstractNumId }
        ) { NumberID = numId });

        return numId;
    }
}
