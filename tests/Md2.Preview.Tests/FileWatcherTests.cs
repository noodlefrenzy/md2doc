// agent-notes: { ctx: "Tests for file watcher with debounce", deps: [FileWatcher], state: active, last: "tara@2026-03-13" }

using Md2.Preview;
using Shouldly;

namespace Md2.Preview.Tests;

public class FileWatcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;

    public FileWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"md2-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "test.md");
        File.WriteAllText(_tempFile, "# Initial");
    }

    [Fact]
    public void Constructor_ThrowsOnNullPath()
    {
        Should.Throw<ArgumentNullException>(() => new FileWatcher(null!, () => { }));
    }

    [Fact]
    public void Constructor_ThrowsOnNullCallback()
    {
        Should.Throw<ArgumentNullException>(() => new FileWatcher(_tempFile, null!));
    }

    [Fact]
    public void DebounceMs_Is300()
    {
        FileWatcher.DebounceMs.ShouldBe(300);
    }

    [Fact]
    public async Task FileChange_InvokesCallback()
    {
        var triggered = new TaskCompletionSource<bool>();
        using var watcher = new FileWatcher(_tempFile, () => triggered.TrySetResult(true));

        // Modify file
        await Task.Delay(50);
        File.WriteAllText(_tempFile, "# Changed");

        // Wait for debounce + callback (300ms debounce + margin)
        var completed = await Task.WhenAny(triggered.Task, Task.Delay(2000));
        completed.ShouldBe(triggered.Task, "Callback should have been invoked");
    }

    [Fact]
    public async Task Dispose_StopsWatching()
    {
        var callCount = 0;
        var watcher = new FileWatcher(_tempFile, () => Interlocked.Increment(ref callCount));
        watcher.Dispose();

        // Modify file after dispose
        await Task.Delay(50);
        File.WriteAllText(_tempFile, "# After dispose");
        await Task.Delay(500); // Wait longer than debounce

        callCount.ShouldBe(0);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var watcher = new FileWatcher(_tempFile, () => { });
        watcher.Dispose();
        Should.NotThrow(() => watcher.Dispose());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
