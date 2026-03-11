// agent-notes: { ctx: "Builds OpenXml Drawing elements for embedded images", deps: [ParagraphBuilder, DocumentFormat.OpenXml, DocumentFormat.OpenXml.Drawing, DocumentFormat.OpenXml.Drawing.Wordprocessing, DocumentFormat.OpenXml.Drawing.Pictures], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;

using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Md2.Emit.Docx;

public sealed class ImageBuilder
{
    private readonly ParagraphBuilder _paragraphBuilder;
    private static uint _imageCounter = 0;

    private const long EmuPerInch = 914400L;
    private const long EmuPerCm = 360000L;

    public ImageBuilder(ParagraphBuilder paragraphBuilder)
    {
        _paragraphBuilder = paragraphBuilder;
    }

    /// <summary>
    /// Returns a placeholder paragraph for missing images.
    /// </summary>
    public Paragraph BuildPlaceholder(string imagePath)
    {
        var paragraph = _paragraphBuilder.CreateBodyParagraph();
        var run = _paragraphBuilder.CreateRun($"[Image not found: {imagePath}]", italic: true);
        paragraph.Append(run);
        return paragraph;
    }

    /// <summary>
    /// Embeds an image into the document, returning a Paragraph containing the Drawing element.
    /// </summary>
    public Paragraph BuildImage(MainDocumentPart mainPart, string imagePath, string altText, ResolvedTheme theme)
    {
        if (!File.Exists(imagePath))
        {
            return BuildPlaceholder(imagePath);
        }

        var contentType = GetImageContentType(imagePath);
        var imagePart = mainPart.AddImagePart(contentType);

        using (var stream = File.OpenRead(imagePath))
        {
            imagePart.FeedData(stream);
        }

        var relId = mainPart.GetIdOfPart(imagePart);

        // Read image dimensions
        var (originalWidth, originalHeight) = GetImageDimensions(imagePath);

        // Calculate max dimensions in EMU
        long maxWidthEmu = (long)(theme.PageWidth - theme.MarginLeft - theme.MarginRight) * EmuPerInch / 1440;
        long maxHeightEmu = (long)(theme.PageHeight - theme.MarginTop - theme.MarginBottom) * EmuPerInch / 1440;

        // Scale to fit
        var (widthEmu, heightEmu) = CalculateScaledDimensions(
            originalWidth, originalHeight, maxWidthEmu, maxHeightEmu);

        var uniqueId = (uint)Interlocked.Increment(ref _imageCounter);

        var drawing = CreateDrawingElement(relId, widthEmu, heightEmu, uniqueId, altText);

        var paragraph = _paragraphBuilder.CreateBodyParagraph();
        paragraph.Append(new Run(drawing));
        return paragraph;
    }

    /// <summary>
    /// Calculates scaled dimensions preserving aspect ratio, fitting within maxWidth and maxHeight.
    /// </summary>
    public static (long width, long height) CalculateScaledDimensions(
        long originalWidth, long originalHeight, long maxWidth, long maxHeight)
    {
        if (originalWidth <= 0 || originalHeight <= 0)
            return (maxWidth, maxHeight);

        double scaleX = originalWidth <= maxWidth ? 1.0 : (double)maxWidth / originalWidth;
        double scaleY = originalHeight <= maxHeight ? 1.0 : (double)maxHeight / originalHeight;
        double scale = Math.Min(scaleX, scaleY);

        long width = (long)Math.Round(originalWidth * scale);
        long height = (long)Math.Round(originalHeight * scale);

        return (width, height);
    }

    private static Drawing CreateDrawingElement(string relId, long widthEmu, long heightEmu, uint uniqueId, string altText)
    {
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                new DW.DocProperties { Id = uniqueId, Name = $"Picture {uniqueId}", Description = altText },
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0, Name = $"image{uniqueId}" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle })
                        )
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            ) { DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0 }
        );
    }

    private static string GetImageContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "image/png"
        };
    }

    private static (long width, long height) GetImageDimensions(string imagePath)
    {
        // Read basic image header for dimensions
        // For simplicity, use a stream-based approach for PNG and JPEG
        try
        {
            using var stream = File.OpenRead(imagePath);
            var header = new byte[24];
            if (stream.Read(header, 0, 24) < 24)
                return (EmuPerInch * 6, EmuPerInch * 4); // default 6x4 inches

            // PNG: width at bytes 16-19, height at bytes 20-23
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                int w = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                int h = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                // Convert pixels to EMU assuming 96 DPI
                return ((long)w * EmuPerInch / 96, (long)h * EmuPerInch / 96);
            }

            // JPEG: need to scan for SOF marker
            if (header[0] == 0xFF && header[1] == 0xD8)
            {
                return GetJpegDimensions(imagePath);
            }

            // Default fallback
            return (EmuPerInch * 6, EmuPerInch * 4);
        }
        catch
        {
            return (EmuPerInch * 6, EmuPerInch * 4);
        }
    }

    private static (long width, long height) GetJpegDimensions(string path)
    {
        using var stream = File.OpenRead(path);
        stream.Position = 2; // skip SOI marker

        while (stream.Position < stream.Length)
        {
            int marker1 = stream.ReadByte();
            if (marker1 != 0xFF) break;

            int marker2 = stream.ReadByte();
            if (marker2 == -1) break;

            // SOF markers: C0, C1, C2
            if (marker2 >= 0xC0 && marker2 <= 0xC2)
            {
                var buf = new byte[7];
                if (stream.Read(buf, 0, 7) < 7) break;
                int height = (buf[3] << 8) | buf[4];
                int width = (buf[5] << 8) | buf[6];
                return ((long)width * EmuPerInch / 96, (long)height * EmuPerInch / 96);
            }

            // Skip this segment
            int lenHi = stream.ReadByte();
            int lenLo = stream.ReadByte();
            if (lenHi == -1 || lenLo == -1) break;
            int segLen = (lenHi << 8) | lenLo;
            stream.Position += segLen - 2;
        }

        return (EmuPerInch * 6, EmuPerInch * 4);
    }
}
