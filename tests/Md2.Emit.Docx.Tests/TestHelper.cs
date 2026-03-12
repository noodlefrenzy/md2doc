// agent-notes: { ctx: "Shared test helper: creates DocxAstVisitor with in-memory document", deps: [Md2.Emit.Docx, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml.Packaging;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;

namespace Md2.Emit.Docx.Tests;

internal static class TestHelper
{
    /// <summary>
    /// Creates a DocxAstVisitor backed by an in-memory WordprocessingDocument.
    /// The returned visitor is usable for testing element generation.
    /// Caller should dispose the returned stream when done.
    /// </summary>
    public static DocxAstVisitor CreateVisitor(ResolvedTheme theme)
    {
        var stream = new MemoryStream();
        var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
            new DocumentFormat.OpenXml.Wordprocessing.Body());

        var paragraphBuilder = new ParagraphBuilder(theme);
        return new DocxAstVisitor(paragraphBuilder, mainPart, theme);
    }
}
