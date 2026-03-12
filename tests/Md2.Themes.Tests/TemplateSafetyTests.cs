// agent-notes: { ctx: "TDD tests for template safety checks", deps: [src/Md2.Themes/TemplateSafetyChecker.cs], state: active, last: "tara@2026-03-12" }

using Shouldly;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class TemplateSafetyTests
{
    [Fact]
    public void Check_ValidDocx_ReturnsPass()
    {
        // Create a minimal valid DOCX (ZIP with PK signature)
        var path = CreateTempFile(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 }, ".docx");
        try
        {
            var result = TemplateSafetyChecker.Check(path);
            result.IsValid.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_IrmProtectedFile_ReturnsError()
    {
        // OLE compound document signature (IRM-protected DOCX)
        var path = CreateTempFile(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, ".docx");
        try
        {
            var result = TemplateSafetyChecker.Check(path);
            result.IsValid.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Contains("IRM") || e.Contains("protected"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_LegacyDocFile_ReturnsError()
    {
        var path = CreateTempFile(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, ".doc");
        try
        {
            var result = TemplateSafetyChecker.Check(path);
            result.IsValid.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Contains(".doc") || e.Contains("legacy"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_DocmFile_ReturnsWarning()
    {
        var path = CreateTempFile(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 }, ".docm");
        try
        {
            var result = TemplateSafetyChecker.Check(path);
            result.IsValid.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Contains(".docm") || e.Contains("macro"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_InvalidSignature_ReturnsError()
    {
        var path = CreateTempFile(new byte[] { 0x00, 0x00, 0x00, 0x00 }, ".docx");
        try
        {
            var result = TemplateSafetyChecker.Check(path);
            result.IsValid.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Contains("valid DOCX") || e.Contains("signature"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_FileTooLarge_ReturnsWarning()
    {
        // Create a file with ZIP signature but > size limit
        var path = CreateTempFile(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, ".docx");
        try
        {
            // Use a low size limit to trigger the check
            var result = TemplateSafetyChecker.Check(path, maxSizeBytes: 2);
            result.IsValid.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.Contains("size") || e.Contains("large"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_FileNotFound_ThrowsFileNotFoundException()
    {
        Should.Throw<FileNotFoundException>(() =>
            TemplateSafetyChecker.Check("/nonexistent/template.docx"));
    }

    [Fact]
    public void Check_EmptyFile_ReturnsError()
    {
        var path = CreateTempFile(Array.Empty<byte>(), ".docx");
        try
        {
            var result = TemplateSafetyChecker.Check(path);
            result.IsValid.ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_DefaultSizeLimit_Is50MB()
    {
        TemplateSafetyChecker.DefaultMaxSizeBytes.ShouldBe(50L * 1024 * 1024);
    }

    private static string CreateTempFile(byte[] content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllBytes(path, content);
        return path;
    }
}
