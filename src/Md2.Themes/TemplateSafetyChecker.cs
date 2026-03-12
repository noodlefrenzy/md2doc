// agent-notes: { ctx: "template safety: IRM detection, .doc rejection, .docm warning, size limit", deps: [docs/adrs/0010-irm-protected-templates.md], state: active, last: "sato@2026-03-12" }

namespace Md2.Themes;

/// <summary>
/// Result of a template safety check.
/// </summary>
public record TemplateSafetyResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>
/// Checks DOCX template files for safety issues before use:
/// IRM/DRM protection, legacy .doc format, macro-enabled .docm, and file size limits.
/// </summary>
public static class TemplateSafetyChecker
{
    /// <summary>Default maximum template file size: 50 MB.</summary>
    public const long DefaultMaxSizeBytes = 50L * 1024 * 1024;

    // ZIP (DOCX) magic bytes
    private static readonly byte[] ZipSignature = { 0x50, 0x4B, 0x03, 0x04 };

    // OLE compound document magic bytes (IRM-protected or legacy .doc)
    private static readonly byte[] OleSignature = { 0xD0, 0xCF, 0x11, 0xE0 };

    /// <summary>
    /// Checks a template file for safety issues.
    /// </summary>
    /// <param name="path">Path to the template file.</param>
    /// <param name="maxSizeBytes">Maximum allowed file size. Defaults to 50 MB.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public static TemplateSafetyResult Check(string path, long maxSizeBytes = DefaultMaxSizeBytes)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template file not found: {path}", path);

        var errors = new List<string>();
        var fileInfo = new FileInfo(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();

        // Check file size
        if (fileInfo.Length > maxSizeBytes)
        {
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
            var limitMb = maxSizeBytes / (1024.0 * 1024.0);
            errors.Add($"Template file is {sizeMb:F1} MB, which exceeds the {limitMb:F0} MB size limit.");
        }

        // Check .docm (macro-enabled)
        if (extension == ".docm")
        {
            errors.Add(
                "Template file is a macro-enabled .docm file. " +
                "Macros are not used for style extraction and may indicate a malicious file. " +
                "Save as .docx in Word to remove macros, or use --allow-macros to override.");
        }

        // Check file signature
        if (fileInfo.Length < 4)
        {
            errors.Add("Template file is too small to be a valid DOCX file.");
            return new TemplateSafetyResult(false, errors);
        }

        var header = new byte[4];
        using (var fs = File.OpenRead(path))
        {
            fs.ReadExactly(header, 0, 4);
        }

        if (header.AsSpan().SequenceEqual(OleSignature))
        {
            if (extension == ".doc")
            {
                errors.Add(
                    $"'{Path.GetFileName(path)}' is a legacy .doc file (pre-OOXML format). " +
                    "md2 requires .docx format. Open in Word and save as .docx first.");
            }
            else
            {
                errors.Add(
                    $"Cannot use '{Path.GetFileName(path)}' — file appears to be IRM/DRM protected. " +
                    "IRM-protected documents are encrypted and cannot be read without authentication " +
                    "against your organization's rights management server.\n\n" +
                    "To use this template with md2:\n" +
                    "  1. Open the file in Microsoft Word (with appropriate permissions)\n" +
                    "  2. File > Info > Protect Document > Restrict Access > Unrestricted Access\n" +
                    "  3. Save the unprotected copy\n" +
                    "  4. Run: md2 theme extract <unprotected-copy.docx> -o theme.yaml\n\n" +
                    "Alternatively, use a built-in preset: md2 report.md --preset corporate -o report.docx");
            }
        }
        else if (!header.AsSpan().SequenceEqual(ZipSignature))
        {
            errors.Add(
                $"'{Path.GetFileName(path)}' does not have a valid DOCX file signature. " +
                "Expected a ZIP-based OOXML file.");
        }

        return new TemplateSafetyResult(errors.Count == 0, errors);
    }
}
