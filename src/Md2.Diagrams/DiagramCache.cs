// agent-notes: { ctx: "Content-hash PNG cache for rendered diagrams", deps: [], state: active, last: "sato@2026-03-13" }

using System.Security.Cryptography;
using System.Text;

namespace Md2.Diagrams;

/// <summary>
/// Caches rendered diagram PNGs by SHA256 content hash of the source.
/// </summary>
public sealed class DiagramCache
{
    private readonly string _cacheDir;
    private readonly string _versionSalt;

    public DiagramCache(string cacheDir, string? versionSalt = null)
    {
        _cacheDir = cacheDir ?? throw new ArgumentNullException(nameof(cacheDir));
        _versionSalt = versionSalt ?? "";
    }

    /// <summary>
    /// Returns the cache file path for the given diagram source (deterministic, based on SHA256).
    /// </summary>
    public string GetCachePath(string source)
    {
        var hash = ComputeHash(source);
        return Path.Combine(_cacheDir, hash + ".png");
    }

    /// <summary>
    /// Returns the cache file path for the given diagram source and theme key (deterministic, based on SHA256).
    /// Different theme keys produce different cache paths for the same source.
    /// </summary>
    public string GetCachePath(string source, string? themeKey)
    {
        var hash = ComputeHash(themeKey != null ? source + "\0" + themeKey : source);
        return Path.Combine(_cacheDir, hash + ".png");
    }

    /// <summary>
    /// Returns true and the path if a cached PNG exists for this source.
    /// </summary>
    public bool TryGetCached(string source, out string? path)
    {
        return TryGetCached(source, null, out path);
    }

    public bool TryGetCached(string source, string? themeKey, out string? path)
    {
        path = GetCachePath(source, themeKey);
        if (File.Exists(path))
            return true;

        path = null;
        return false;
    }

    /// <summary>
    /// Writes PNG data to the cache. Creates the cache directory if needed.
    /// Returns the stored file path.
    /// </summary>
    public string Store(string source, byte[] pngData)
    {
        return Store(source, null, pngData);
    }

    public string Store(string source, string? themeKey, byte[] pngData)
    {
        Directory.CreateDirectory(_cacheDir);
        var path = GetCachePath(source, themeKey);
        File.WriteAllBytes(path, pngData);
        return path;
    }

    private string ComputeHash(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_versionSalt + source));
        return Convert.ToHexStringLower(bytes);
    }
}
