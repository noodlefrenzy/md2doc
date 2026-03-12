// agent-notes: { ctx: "Red-phase tests for DiagramCache content-hash PNG caching", deps: [Md2.Diagrams.DiagramCache, Shouldly], state: red, last: "tara@2026-03-12" }

using Shouldly;

namespace Md2.Diagrams.Tests;

public class DiagramCacheTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly DiagramCache _cache;

    public DiagramCacheTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "md2-cache-test-" + Guid.NewGuid().ToString("N"));
        _cache = new DiagramCache(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public void GetCachePath_ReturnsDeterministicPath()
    {
        var source = "graph TD; A-->B;";

        var path1 = _cache.GetCachePath(source);
        var path2 = _cache.GetCachePath(source);

        path1.ShouldBe(path2);
    }

    [Fact]
    public void GetCachePath_DifferentSource_ReturnsDifferentPath()
    {
        var path1 = _cache.GetCachePath("graph TD; A-->B;");
        var path2 = _cache.GetCachePath("graph TD; C-->D;");

        path1.ShouldNotBe(path2);
    }

    [Fact]
    public void TryGetCached_ReturnsFalse_WhenNotCached()
    {
        var result = _cache.TryGetCached("graph TD; A-->B;", out var path);

        result.ShouldBeFalse();
        path.ShouldBeNull();
    }

    [Fact]
    public void Store_WritesPngAndTryGetCached_ReturnsTrue()
    {
        var source = "graph TD; A-->B;";
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG magic bytes

        var storedPath = _cache.Store(source, pngData);

        storedPath.ShouldNotBeNullOrEmpty();
        File.Exists(storedPath).ShouldBeTrue();
        File.ReadAllBytes(storedPath).ShouldBe(pngData);

        var found = _cache.TryGetCached(source, out var cachedPath);
        found.ShouldBeTrue();
        cachedPath.ShouldBe(storedPath);
    }

    [Fact]
    public void Store_CreatesDirectoryIfNotExists()
    {
        var nestedDir = Path.Combine(_cacheDir, "nested", "deep");
        var cache = new DiagramCache(nestedDir);
        var source = "graph TD; A-->B;";
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        Directory.Exists(nestedDir).ShouldBeFalse();

        cache.Store(source, pngData);

        Directory.Exists(nestedDir).ShouldBeTrue();
    }

    [Fact]
    public void GetCachePath_ContainsSha256Hash()
    {
        var source = "graph TD; A-->B;";

        var path = _cache.GetCachePath(source);

        // SHA256 hex is 64 chars; the filename should be <64-char-hex>.png
        var fileName = Path.GetFileName(path);
        fileName.ShouldEndWith(".png");

        var hashPart = Path.GetFileNameWithoutExtension(fileName);
        hashPart.Length.ShouldBe(64);
        hashPart.ShouldMatch("^[0-9a-f]{64}$");
    }
}
