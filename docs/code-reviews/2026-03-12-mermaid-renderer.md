---
agent-notes:
  ctx: "Review of MermaidRenderer and DiagramCache for Issue #28"
  deps: [src/Md2.Diagrams/MermaidRenderer.cs, src/Md2.Diagrams/DiagramCache.cs, src/Md2.Diagrams/BrowserManager.cs, tests/Md2.Diagrams.Tests/MermaidRendererTests.cs, tests/Md2.Diagrams.Tests/DiagramCacheTests.cs]
  state: active
  last: "code-reviewer@2026-03-12"
---
# Code Review: MermaidRenderer with PNG output and content-hash caching

**Date:** 2026-03-12
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Diagrams/MermaidRenderer.cs`
- `src/Md2.Diagrams/DiagramCache.cs`
- `src/Md2.Diagrams/BrowserManager.cs` (context -- prior wave, first real consumer)
- `src/Md2.Diagrams/Md2.Diagrams.csproj`
- `tests/Md2.Diagrams.Tests/MermaidRendererTests.cs`
- `tests/Md2.Diagrams.Tests/DiagramCacheTests.cs`

**Verdict:** Changes requested

## Context

Issue #28 adds the ability to render Mermaid diagram code blocks to PNG images via Playwright/Chromium. The implementation has two classes: `DiagramCache` (content-hash-based file cache using SHA256) and `MermaidRenderer` (loads an embedded mermaid.min.js into a headless Chromium page, waits for SVG rendering, screenshots to PNG). ADR-0008 guides the design.

The architecture is sound: embedded JS avoids CDN dependency, one browser instance is reused across diagrams, and caching prevents redundant renders. The review focuses on concurrency safety, cancellation semantics, test gating, and supply chain documentation.

## Findings

### Critical

No critical findings.

### Important

#### 1. BrowserManager.GetBrowserAsync has a race condition

**File:** `src/Md2.Diagrams/BrowserManager.cs`, lines 56-71
**What:** The null-check `if (_browser is not null) return _browser` is not protected by any synchronization primitive. The doc comment claims "Thread-safe via lazy init" but this is plain check-then-act.
**Why it matters:** ADR-0008 section 7 explicitly calls for parallel diagram rendering. Two concurrent `RenderAsync` calls on a cold `BrowserManager` will launch two Chromium processes. One will be orphaned -- never disposed, leaking a 300MB+ process. In CI, this could cause flaky test failures or resource exhaustion.
**Fix:** Replace the null-check with `SemaphoreSlim(1,1)` guarding the init block, or use `Lazy<Task<IBrowser>>` with `LazyThreadSafetyMode.ExecutionAndPublication`.

#### 2. OperationCanceledException is swallowed

**File:** `src/Md2.Diagrams/MermaidRenderer.cs`, lines 106-116
**What:** The catch structure rethrows `Md2ConversionException` but wraps everything else (including `OperationCanceledException`) into a new `Md2ConversionException`.
**Why it matters:** When a user cancels (Ctrl+C), the caller receives a conversion error instead of a cancellation. This breaks standard .NET cancellation patterns -- upstream code checking `catch (OperationCanceledException)` will never fire.
**Fix:** Add `catch (OperationCanceledException) { throw; }` before the generic catch block.

#### 3. Cache hash does not include Mermaid JS version

**File:** `src/Md2.Diagrams/DiagramCache.cs`, line 54
**What:** The SHA256 hash is computed solely from the Mermaid source string.
**Why it matters:** When `mermaid.min.js` is upgraded, stale cached PNGs will silently serve old renderings. This is especially subtle because the cache directory persists across tool upgrades. A developer updates md2, re-runs on the same document, and gets old images.
**Fix:** Salt the hash with a version string or the assembly version. Even a static constant like `private const string CacheVersion = "mermaid-11.13.0";` prepended to the source before hashing would suffice.

#### 4. Integration tests need a skip guard

**File:** `tests/Md2.Diagrams.Tests/MermaidRendererTests.cs`
**What:** All five tests launch Chromium with no gating mechanism.
**Why it matters:** ADR-0008 itself notes "Must be gated behind an environment flag or test category." A developer cloning the repo and running `dotnet test` will get five unexplained failures if Chromium is not installed. This creates a terrible first-contribution experience.
**Fix:** Add `[Trait("Category", "Integration")]` to each test (or the class), and document the test filter in the README. Alternatively, add a static skip check using `BrowserManager.IsChromiumInstalled()`.

#### 5. No guard or test for null/empty diagram source

**File:** `src/Md2.Diagrams/MermaidRenderer.cs`, line 33
**What:** `RenderAsync` accepts any string including null, empty, or whitespace.
**Why it matters:** An empty string passed to Mermaid JS will produce a valid (but meaningless) SVG, which gets cached permanently. A null will throw a `NullReferenceException` from deep inside `DiagramCache.GetCachePath`, with no useful error message.
**Fix:** Add `ArgumentException.ThrowIfNullOrWhiteSpace(mermaidSource)` at the top of `RenderAsync`, and add a test for this guard.

### Suggestions

#### 6. Use WebUtility.HtmlEncode instead of manual escaping

**File:** `src/Md2.Diagrams/MermaidRenderer.cs`, lines 126-130
**What:** Manual `Replace` calls handle four of five HTML entities (missing single quote).
**Why it matters:** Not exploitable in the current context (content position, local page load), but `System.Net.WebUtility.HtmlEncode` is one call, covers all entities, and is less error-prone if the template evolves.

#### 7. Add SBOM entry for embedded mermaid.min.js

**File:** `src/Md2.Diagrams/Resources/mermaid.min.js` (2.9MB)
**What:** No checksum or provenance record exists for this file.
**Why it matters:** A future developer or auditor cannot verify that this is an unmodified copy of mermaid v11.13.0. For a file embedded directly in the assembly, this is a supply chain documentation gap. A `docs/sbom/mermaid-js.md` with version, download URL, and SHA256 would close this.

#### 8. Misleading log message in BrowserManager

**File:** `src/Md2.Diagrams/BrowserManager.cs`, line 69
**What:** `_browser.Contexts.Count` is logged as `{ProcessId}`. It will always be 0 at browser launch time.
**Fix:** Remove the misleading parameter or replace with actual useful info (e.g., just log "Chromium browser launched" without a fake PID).

#### 9. DiagramCache.Store -- consider validating pngData

**File:** `src/Md2.Diagrams/DiagramCache.cs`, line 46
**What:** `null` or empty `byte[]` would create a zero-byte file or throw an unguarded NullReferenceException.
**Fix:** Guard clause plus a test for the boundary.

## Lessons

1. **Check-then-act is never thread-safe.** The pattern `if (x == null) { x = create(); } return x;` is the canonical data race. In async code, use `SemaphoreSlim` or `Lazy<Task<T>>`. The comment "Thread-safe via lazy init" made the bug harder to spot because it gave reviewers false confidence. If you write a thread-safety comment, verify the mechanism actually provides it.

2. **Never swallow OperationCanceledException.** In .NET, cancellation flows through exceptions. If your catch-all block wraps `OperationCanceledException` into a domain exception, the entire CancellationToken contract breaks for every caller up the stack. The standard pattern is: rethrow your domain exceptions, rethrow cancellation, then catch everything else. Three catch blocks, in that order.

3. **Content-addressed caches need a version salt.** Hashing only the input content is correct until your rendering engine changes. Then the cache silently serves stale results. This is a subtle bug because it only manifests after upgrades and the output "looks right" until you diff carefully. Salt the hash with the tool version, the renderer version, or a cache-format version constant.

4. **Integration tests that require external binaries must be skippable.** If `dotnet test` fails out of the box because Chromium is not installed, new contributors will either waste time debugging or skip your tests forever. Gate them behind a trait, a skip condition, or both.

5. **Embedded opaque binaries need provenance records.** A 2.9MB minified JS file cannot be reviewed by humans. The mitigation is documentation: version, source URL, SHA256, and a script to reproduce the download. This is not paranoia -- it is the minimum bar for supply chain hygiene on embedded resources.
