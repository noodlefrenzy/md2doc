// agent-notes: { ctx: "File watcher for hot-reload preview", deps: [System.IO.FileSystemWatcher], state: active, last: "sato@2026-03-13" }

namespace Md2.Preview;

/// <summary>
/// Watches a Markdown file for changes and invokes a callback on modification.
/// Debounces rapid changes (e.g., from editors that write temp files then rename).
/// </summary>
public sealed class FileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action _onChange;
    private readonly Timer _debounceTimer;
    private bool _disposed;

    /// <summary>Debounce interval in milliseconds.</summary>
    public const int DebounceMs = 300;

    public FileWatcher(string filePath, Action onChange)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(onChange);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException($"Cannot determine directory for: {filePath}");
        var fileName = Path.GetFileName(fullPath);

        _onChange = onChange;
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Reset debounce timer
        _debounceTimer.Change(DebounceMs, Timeout.Infinite);
    }

    private void OnDebounceElapsed(object? state)
    {
        try
        {
            _onChange();
        }
        catch
        {
            // Swallow errors from callback to avoid crashing the timer thread
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
