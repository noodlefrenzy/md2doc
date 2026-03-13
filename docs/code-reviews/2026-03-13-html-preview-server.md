---
agent-notes:
  ctx: "Review of HTML preview server with hot-reload"
  deps: [src/Md2.Preview/PreviewServer.cs, src/Md2.Preview/HtmlPreviewRenderer.cs, src/Md2.Preview/FileWatcher.cs, src/Md2.Preview/PreviewSession.cs]
  state: active
  last: "code-reviewer@2026-03-13"
---
# Code Review: HTML Preview Server with Hot-Reload (Issue #51)

**Date:** 2026-03-13
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Commit:** f1d78e9
**Files reviewed:** `src/Md2.Preview/PreviewServer.cs`, `src/Md2.Preview/HtmlPreviewRenderer.cs`, `src/Md2.Preview/FileWatcher.cs`, `src/Md2.Preview/PreviewSession.cs`, `tests/Md2.Preview.Tests/PreviewServerTests.cs`, `tests/Md2.Preview.Tests/HtmlPreviewRendererTests.cs`, `tests/Md2.Preview.Tests/FileWatcherTests.cs`, `docs/adrs/0012-html-preview-server.md`
**Verdict:** Changes requested

## Context

Issue #51 adds a live preview mode to md2. The implementation includes an embedded HTTP server (`PreviewServer`), a theme-aware HTML renderer (`HtmlPreviewRenderer`), a debounced file watcher (`FileWatcher`), and an orchestrator (`PreviewSession`) that ties them together with a Playwright-controlled Chromium window. The HTML page includes inline JavaScript that polls the server for version changes and hot-swaps the content div without a full reload. ADR-0012 documents the architecture decision.

## Findings

### Critical

**C1. CSS injection via unsanitized theme font names**

*File:* `HtmlPreviewRenderer.cs`, lines 91-92, 120-121
*Lens:* Pierrot (security)

Theme properties like `BodyFont`, `HeadingFont`, `MonoFont` are interpolated directly into CSS string literals:

```csharp
font-family: '{{theme.BodyFont}}', 'Georgia', serif;
```

A YAML theme file with a font name containing `'` can break out of the CSS string context and inject arbitrary CSS rules. While this is a localhost-only server (limiting the blast radius), it violates defense-in-depth. If the preview feature is ever exposed in a multi-user context or the HTML is saved/shared, this becomes a real vulnerability.

*Fix:* Sanitize font names to `[a-zA-Z0-9 _-]` or apply CSS escaping before interpolation. Consider the same for color hex values, although their character range makes exploitation harder.

*Principle:* Never interpolate user-controlled strings into code contexts (CSS, HTML, SQL, shell) without escaping. Even when the immediate context seems safe, defense-in-depth protects against future changes that widen the attack surface.

**C2. Torn reads on content fields -- race condition**

*File:* `PreviewServer.cs`, lines 33-37
*Lens:* Vik (concurrency)

`UpdateContent` writes `_currentHtml` and `_currentBodyHtml` as two separate assignments, then atomically increments `_version`. A concurrent request handler can observe an inconsistent state: new HTML but old body, or new version number with old content. The `Interlocked.Increment` on the version gives a false sense of thread safety.

*Fix:* Bundle all three values into an immutable snapshot object and swap atomically:

```csharp
private record ContentSnapshot(string FullHtml, string BodyHtml, long Version);
private volatile ContentSnapshot _content = new("", "", 0);

public void UpdateContent(string fullHtml, string bodyHtml)
{
    _content = new ContentSnapshot(fullHtml, bodyHtml, _content.Version + 1);
}
```

*Principle:* When multiple related values must be consistent from a reader's perspective, they need to change atomically. `Interlocked` on one field does not protect the others. The immutable-snapshot pattern is the simplest fix when there is one writer and many readers.

### Important

**I1. Silent exception swallowing in FileWatcher callback**

*File:* `FileWatcher.cs`, lines 48-55
*Lens:* Vik (maintainability), Ines (observability)

The bare `catch` in `OnDebounceElapsed` swallows every exception from the callback, including fatal exceptions and legitimate render errors. The user sees the preview stop updating with zero feedback.

*Fix:* Catch `Exception` (not bare `catch`), propagate to a logging mechanism, and consider re-throwing truly fatal exceptions (`OutOfMemoryException`, etc.). Either accept a logger in the constructor or ensure the callback itself handles logging.

**I2. Double markdown parse on every file change**

*File:* `PreviewSession.cs`, lines 93-108
*Lens:* Vik (performance)

`RenderAndUpdate` calls `Markdown.Parse()` to get body HTML, then calls `RenderFromSource()` which parses the same text again. For large documents, this doubles the render time on every save.

*Fix:* Parse once, then use the `Render(document, theme, pipeline)` overload for the full page. Or refactor the renderer to return both full and body HTML from a single parse.

**I3. No tests for PreviewSession**

*Lens:* Tara (coverage)

`PreviewSession` is the orchestrator and contains the most integration-sensitive logic: lifecycle ordering, the `RenderAndUpdate` method (with its double-parse and IOException handling), disposal sequencing. There are zero tests for it. If any of these components fail to wire together correctly, no test catches it.

*Fix:* At minimum, extract `RenderAndUpdate` so it can be unit-tested. For the full lifecycle, consider an integration test that starts a session, modifies the watched file, and asserts the server content updates (without Playwright -- just verify the HTTP responses).

**I4. Request handler swallows all exceptions silently**

*File:* `PreviewServer.cs`, lines 104-107
*Lens:* Vik + Ines (observability)

The catch-all around the request handler means any unexpected error (serialization failure, encoding issue) disappears silently. The server appears to work but returns nothing.

*Fix:* Log the exception and return a 500 response so the polling script and any debugging can detect failures.

**I5. TOCTOU race in port selection**

*File:* `PreviewServer.cs`, lines 119-126
*Lens:* Vik (robustness)

`FindAvailablePort` opens and closes a TCP listener to find a free port, then `HttpListener` tries to bind later. Another process can claim the port in between. This is a known limitation of `HttpListener` (which does not support port-0 auto-assignment), but it should be documented and ideally wrapped in a retry loop.

### Suggestions

- The 200ms polling interval in the reload script could use exponential backoff when idle to reduce CPU churn during long editing pauses.
- Consider adding a comment on the `localhost`-only binding noting it is intentional for security.
- `FileWatcherTests.FileChange_InvokesCallback` uses `Task.Delay(50)` before writing. This is a minor flaky-test risk in CI; the 2-second timeout mitigates it but worth being aware of.

## Lessons

1. **Atomic consistency for related mutable state.** When multiple fields must be read consistently by concurrent threads, `Interlocked` on one field is not enough. The immutable-snapshot-with-volatile-swap pattern is the simplest correct solution for single-writer/multi-reader scenarios. The key question: "Can a reader ever see field A from version N and field B from version N+1?" If yes, you have a torn-read bug.

2. **Never interpolate into code contexts without escaping.** CSS, HTML, SQL, shell -- any time you embed a value into a language that has its own syntax, you need context-appropriate escaping. String interpolation in C# raw strings feels safe because it looks like a template, but the target language's parser does not care how the string was constructed. Even in "safe" contexts like localhost servers, defense-in-depth matters because code gets reused in ways the author did not anticipate.

3. **Silent exception swallowing is a maintenance trap.** Bare `catch` or `catch (Exception)` with no logging creates code that "works" until it does not, and then gives you nothing to debug. Timer callbacks and background threads are particularly dangerous because the exception has nowhere visible to go. Always log, even if you cannot re-throw.

4. **Test the orchestrator, not just the components.** Unit tests for individual components (server, renderer, watcher) are valuable, but the orchestrator is where integration bugs live -- lifecycle ordering, error propagation, resource cleanup. If the orchestrator is hard to test, that is a design signal that it may be doing too much or that its dependencies need seams.

5. **Avoid redundant work in hot paths.** The double-parse in `RenderAndUpdate` is easy to miss because the two calls look independent. When a method is called on every file save, even small inefficiencies compound. Review hot-path methods by tracing the full call chain to spot redundant computation.
