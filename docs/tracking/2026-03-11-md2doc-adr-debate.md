---
agent-notes: { ctx: "adversarial debate tracking for md2 ADRs", deps: [docs/adrs/, docs/architecture.md], state: active, last: "archie@2026-03-11" }
---

# ADR Debate: md2

**Date:** 2026-03-11
**Challenger:** Wei
**Architecture Lead:** Archie
**Status:** Complete -- awaiting team response

---

## ADR-0003: Use Markdig as the Markdown Parser

**Challenges:**

1. **[Assumption surfacing]:** The ADR states "Markdig is the de facto standard in .NET" as if this settles the question. But the real question it avoids is: why are we not building a pandoc wrapper or pandoc filter instead of reimplementing an entire conversion pipeline? The discovery doc says "output quality must be noticeably superior to pandoc" -- but we could achieve that by writing a custom pandoc writer that produces better DOCX.

2. **[Cost of being wrong]:** Bus factor of one (Alexandre Mutel). "Microsoft ecosystem adoption creates implicit maintenance pressure" is not a maintenance contract. The real mitigation is that Markdig's API surface is stable enough that a frozen library would still work for years. State this explicitly.

**Severity:** Minor
**Recommendation:** Accept as-is
**Rationale:** No viable alternative in .NET. The "why not pandoc" question was resolved in discovery -- user chose C#/.NET and local-only. Bus factor is real but low-impact given stability.

---

## ADR-0004: Use Open XML SDK for DOCX/PPTX Generation

**Challenges:**

1. **[Cost of being wrong]:** "No layout engine" is acknowledged but not quantified. Manual table column width calculation, content-aware page breaking, and widow/orphan control are each significant efforts. GemBox.Document at $890 would likely save 200+ hours. The ADR dismisses it with "no license cost" but the human pays the development time cost instead.

2. **[Historical precedent]:** `markdig.docx` is cited as proof of viability but "covers only basic elements." This is the pattern with Open XML SDK projects: they work for simple docs and hit a wall on complex tables, nested lists, math, and style inheritance. The hard 80% is producing correct OOXML, not parsing Markdown.

**Severity:** Important
**Recommendation:** Accept with modifications
**Modifications:**
- Add a risk item acknowledging "no layout engine" as the single largest implementation risk
- Time-box table auto-sizing prototype as a go/no-go gate
- If table auto-sizing takes more than N days to reach "good enough," revisit GemBox
- Name the specific OOXML spec sections that need careful implementation

---

## ADR-0005: Use Markdig's Native AST (No Custom IR)

**Challenges:**

1. **[Inversion]:** What if you DO need a custom IR halfway through implementation, when the DOCX emitter is tightly coupled to Markdig node types? The ADR says "cost of introducing an IR later is bounded." This is optimistic. The emitter will have dozens of visitor methods reaching deep into Markdig-specific properties. The translation step is not a thin adapter -- it is an emitter rewrite.

2. **[Assumption surfacing]:** `SetData(key, object)` as a substitute for typed domain properties. You're building a pipeline with 5+ annotation types. Constant keys with object values is a stringly-typed API. A typed wrapper (~200 lines) would eliminate an entire class of runtime errors.

3. **[Scale attack]:** Five transforms today, more in v2. Each reads/writes `SetData()` values others depend on. Without typed contracts, the implicit dependency graph is invisible. Transform ordering bugs will be the hardest to diagnose.

**Severity:** Important
**Recommendation:** Accept with modifications
**Modifications:** Build strongly-typed extension methods over Markdig AST:
```csharp
public static class AstExtensions
{
    public static IReadOnlyList<SyntaxToken>? GetSyntaxTokens(this MarkdownObject node) => ...;
    public static void SetSyntaxTokens(this MarkdownObject node, IReadOnlyList<SyntaxToken> tokens) => ...;
}
```
Costs almost nothing, provides compile-time safety, documents inter-transform contracts.

---

## ADR-0006: Custom LaTeX-to-OMML Converter with PNG Fallback

**Challenges:**

1. **[Alternative technology]:** The ADR dismisses LaTeX → MathML → OMML via XSLT as "adds complexity." But this is the approach that works in the real world. Microsoft ships `MML2OMML.xsl` with Office. Multiple projects use this pipeline. LaTeX-to-MathML is well-solved. MathML-to-OMML is Microsoft's own XSLT. By contrast, a direct LaTeX-to-OMML converter has zero precedent in C#.

2. **[Cost of being wrong]:** If the OMML converter fails, PNG fallback is a fundamentally different product. PNG math is not editable, doesn't scale with text, doesn't respect document fonts. The "90%+ of math in typical documentation" claim for the supported subset is unsubstantiated. Aligned equations and cases environments are common in real technical docs.

3. **[Assumption surfacing]:** "OMML is relatively simple for basic math" underestimates the spec. Even `\frac{a}{b}` requires constructing nested elements with run properties, font handling, and spacing.

4. **[Historical precedent]:** pandoc's `texmath` is 11,000+ lines of Haskell for this conversion, maintained for over a decade. Estimating 1000-2000 lines for a C# subset is plausible only with significant fidelity gaps.

**Severity:** Critical
**Recommendation:** Reconsider
**Counter-proposal:** Adopt the MathML intermediate path as primary:
1. Use KaTeX (via Playwright, already available) or a .NET MathML library for LaTeX → MathML
2. Apply Microsoft's `MML2OMML.xsl` via `System.Xml.Xsl.XslCompiledTransform` (built into .NET)
3. Dramatically less custom code, proven components
4. If insisting on direct converter: prototype BOTH paths, time-box, define explicit acceptance criteria

---

## ADR-0007: Use TextMateSharp for Syntax Highlighting

**Challenges:**

1. **[Assumption surfacing]:** The ADR says "no native dependencies (pure C#)" but TextMateSharp wraps Oniguruma (native binary). This matters for deployment, single-file publish, trimming, and exotic Linux distros.

2. **[Alternative technology]:** Shiki via Playwright (already in the dependency graph) uses the same TextMate grammars with identical quality and no native dependency. The ADR did not evaluate this option.

**Severity:** Minor
**Recommendation:** Accept with modifications
**Modifications:** Document Shiki-via-Playwright as a fallback. Explicitly test single-file publish and Alpine Linux with the Oniguruma native binary. Rationale for TextMateSharp over Shiki: syntax highlighting should work without Chromium installed.

---

## ADR-0008: Use Playwright for .NET for Mermaid Rendering

**Challenges:**

1. **[Scale attack]:** 20 unique Mermaid diagrams in one document. Even with page-level parallelization, what's the total conversion time? "Mermaid rendering is slow" is not a sufficient analysis. Need benchmarks.

2. **[Assumption surfacing]:** Bundled Mermaid JS (~2MB) pins users to md2's release cycle for rendering fixes. No way to update Mermaid independently.

3. **[Cost of being wrong]:** Playwright pins Chromium to specific OS library versions. Known breakage on Linux when system libraries evolve. For a "daily driver CLI" this is a real usability risk.

**Severity:** Important
**Recommendation:** Accept with modifications
**Modifications:**
- Add `--mermaid-js <path>` flag for user-supplied Mermaid JS
- Define benchmarking acceptance criteria (10 diagrams in under N seconds)
- Document Chromium-on-Linux compatibility and supported distros
- Consider SVG intermediate output as escape hatch when Playwright unavailable

---

## ADR-0009: YAML Theme DSL with 4-Layer Cascade

**Challenges:**

1. **[Scale attack]:** User runs `--template corp.docx --preset technical --theme overrides.yaml --style "heading1.fontSize=24pt"` and heading 1 looks wrong. How do they debug a 4-layer trace for 30+ properties? You need a `md2 theme debug` command. The CSS cascade is powerful and also the source of enormous developer frustration.

2. **[Assumption surfacing]:** `${...}` variable interpolation is a custom DSL bolted onto YAML. YAML already has anchors/aliases. Why invent a second reference mechanism that needs cycle detection, type coercion, and error reporting?

3. **[Inversion]:** What if you dropped YAML themes for v1 and only shipped presets + CLI overrides + template? Presets cover common cases. `--style` covers surgical overrides. `--template` covers corporate needs. What user story requires a full YAML theme that these three can't satisfy?

4. **[Cost of being wrong]:** Schema versioning means committing before you have real-world usage data. Every v1 property is a property you support forever.

**Severity:** Important
**Recommendation:** Accept with modifications
**Modifications:**
- Add `md2 theme resolve` / `md2 theme debug` command (essential for usability)
- Consider dropping `${...}` interpolation for v1 (plain values, add later without breaking schema)
- Design schema to be additive (unknown properties ignored for forward compatibility)
- The "drop YAML themes for v1" inversion deserves genuine discussion; document the decision either way

---

## ADR-0010: Fail-Fast with Guidance for IRM-Protected Templates

**Challenges:**

1. **[Assumption surfacing]:** OLE compound documents are also used by legacy `.doc` files and password-protected (non-IRM) DOCX files. Detection via file header conflates three different cases. A password-protected `.docx` would get an IRM error message, which is wrong.

2. **[Inversion]:** Password-protected DOCX files CAN be opened with the password. Should md2 support `--template-password`? This is a real enterprise use case.

**Severity:** Minor
**Recommendation:** Accept with modifications
**Modifications:**
- Distinguish IRM-encrypted, password-encrypted, and legacy `.doc` in detection logic
- Each gets a different error message and guidance
- Consider `--template-password` for simple password protection
- Document why 50MB size limit was chosen, or make it configurable

---

## ADR-0011: Use System.CommandLine for CLI Framework

**Challenges:**

1. **[Alternative technology]:** System.CommandLine has been pre-release for 5+ years. Spectre.Console.Cli is GA, actively maintained, and the ADR already plans to use Spectre.Console for output. Why use two libraries when one handles both?

2. **[Cost of being wrong]:** "API surface has changed between previews." If it makes breaking changes at GA, the migration cost is a rewrite of all command definitions.

**Severity:** Minor
**Recommendation:** Accept with modifications
**Modifications:**
- Document Spectre.Console.Cli as fallback
- Isolate command definitions behind abstractions
- Evaluate whether Spectre.Console.Cli satisfies all needs; document why it was rejected if so

---

## Summary

### Critical Findings

| ADR | Severity | Action |
|-----|----------|--------|
| 0006 (LaTeX-to-OMML) | Critical | Reconsider: MathML intermediate path is proven, direct converter has zero C# precedent |
| 0004 (Open XML SDK) | Important | Time-boxed prototype gate for table auto-sizing |
| 0005 (Native AST) | Important | Add typed accessor layer over SetData/GetData |
| 0009 (Theme DSL) | Important | Add debug tooling, consider deferring `${...}` interpolation |
| 0008 (Playwright) | Important | Benchmarks, `--mermaid-js` override, Linux compat docs |

### Two Biggest Risks to Project Success

1. **ADR-0006 (LaTeX-to-OMML):** Building a custom converter with no C# precedent is the riskiest decision. The MathML intermediate path (LaTeX → MathML via KaTeX, MathML → OMML via Microsoft's XSLT) is battle-tested. Prototype both, time-box, decide.

2. **ADR-0004 (Open XML SDK):** "No layout engine" will be the source of the most tedious implementation work. Table auto-sizing prototype is the go/no-go gate.

### Honest Assessment

The architecture is fundamentally sound. Pipeline model is clean, emitter abstraction is well-designed, risks are identified. The team is not ignoring problems. But ADR-0006 mitigations need to be stronger than "prototype early and fall back to PNG."

---

## Archie's Responses

**Date:** 2026-03-11
**Status:** Complete

---

### ADR-0006 (Critical): LaTeX-to-OMML -- MathML Intermediate Path

**Verdict: Accept. Wei is right. I am revising the ADR.**

This is the one where I have to eat my words. I dismissed the MathML intermediate path as "adds complexity (two conversion steps, XSLT dependency)" and that was a poor evaluation. Let me lay out why Wei's challenge changed my mind:

**What I got wrong:**

1. I treated "two conversion steps" as a cost without weighing it against "building an entirely novel converter with zero C# precedent." Two well-understood steps composed together are far less risky than one step nobody has done before.

2. I called `MML2OMML.xsl` an "XSLT dependency" as if that were exotic. `System.Xml.Xsl.XslCompiledTransform` ships in the .NET base class library. It is not a dependency -- it is the runtime we already have.

3. Wei's point about the PNG fallback being "a fundamentally different product" is correct. I was treating PNG as an acceptable fallback, but non-editable, non-scaling math images in a tool whose reason to exist is "noticeably superior to pandoc" is a contradiction. The fallback must be rare, not the expected path for anything beyond simple fractions.

4. I checked: KaTeX outputs MathML by default alongside its HTML output. The `renderToString` function produces MathML in a `<math>` element. We already have Playwright for Mermaid. Using it to run KaTeX server-side for the LaTeX-to-MathML step means zero custom parsing of LaTeX -- a problem that pandoc's `texmath` needed 11,000 lines to solve.

**What the revised approach looks like:**

```
LaTeX string
  --> KaTeX (via Playwright, MathML output mode)
  --> MathML XML
  --> MML2OMML.xsl (via XslCompiledTransform)
  --> OMML elements
  --> inserted into DOCX
```

**Licensing concern I investigated:** The `MML2OMML.xsl` file ships with Office and is redistributed widely in open-source projects (transpect/docx_modify-lib, meTypeset, libtex2omml, among others). The file's origins trace back to David Carlisle's work for the W3C, and it has been redistributed by many open-source projects under their own licenses. Multiple open-source projects on GitHub include it directly. We will include the file with appropriate attribution and verify the licensing during implementation.

**Dependency on Playwright:** This does mean that math rendering requires Playwright/Chromium, same as Mermaid. For documents with math but no Mermaid, users must still install Chromium. This is acceptable because: (a) math and Mermaid are both P1 features targeting technical documentation users who are likely to use both, (b) the product context says "accept heavy dependencies if they serve the quality bar," and (c) the Chromium download is already a one-time cost.

**What about air-gap without Chromium?** For the case where a user has no Chromium installed and no network access, and their document contains LaTeX math, the behavior is: warn that math rendering requires Chromium, skip the math blocks (leaving them as code blocks or plaintext), and suggest `--no-math` to suppress the warning. This is an edge case, not the primary path.

**What about the custom converter?** I am not fully abandoning the idea of a direct converter for simple expressions as a future optimization (skip Playwright for inline `$x^2$`). But it is no longer the primary path. It becomes an optional fast-path for common cases, implemented only if Playwright latency for math proves to be a bottleneck.

**Changes to ADR-0006:** Rewritten. MathML intermediate path is now the primary approach. Custom direct converter is demoted to "future optimization."

---

### ADR-0004 (Important): Open XML SDK -- Table Auto-Sizing Prototype Gate

**Verdict: Partially accept. Wei's framing is right but his fallback suggestion has a flaw.**

**What I accept:**

1. "No layout engine" is the single largest implementation risk. The ADR acknowledged it as a negative consequence but did not give it the prominence it deserves. Wei is correct that this needs to be elevated to a first-class go/no-go gate.

2. Time-boxing the table auto-sizing prototype is the right call. I will add a concrete gate: 5 working days to produce a prototype that handles the following table types at "good enough" quality: (a) simple uniform tables, (b) tables with varying content lengths, (c) tables with a header row, (d) tables with one very long cell. If 5 days cannot produce acceptable results for these four cases, we reassess.

3. Naming specific OOXML spec sections is good practice. I will add references.

**What I partially reject:**

Wei suggests GemBox.Document at $890 as the fallback. The problem is that GemBox gives us a higher-level API but takes away the low-level control we need for the quality bar. GemBox generates correct documents, but its formatting options are constrained by its API surface. If GemBox cannot produce the exact style inheritance, custom table borders, or alternating row shading that our theme system demands, we are stuck with a library that does 80% of what we need and blocks the last 20%.

The real fallback is not "switch to GemBox" -- it is "build a table width calculation heuristic that is good enough and improve it iteratively." Table auto-sizing is not binary pass/fail. A character-counting heuristic that gets widths within 10% of optimal is "good enough" for v1. Pixel-perfect auto-sizing is a P2 polish item.

However, I will add GemBox as a documented option to revisit if the prototype reveals that the problem is not just table sizing but the overall complexity of constructing correct OOXML across many element types. If the sheer volume of boilerplate makes the project timeline untenable, GemBox's higher-level API could be worth the trade-off.

**Changes to ADR-0004:** Added prototype gate, specific OOXML spec references, and GemBox as a named fallback with criteria for when to revisit.

---

### ADR-0005 (Important): Native AST -- Typed Accessor Layer

**Verdict: Accept. This is obviously correct and I should have included it from the start.**

Wei's challenge is the mildest possible version of "you are building a stringly-typed API and pretending constant keys fix it." He is right. The cost of typed extension methods is negligible (one static class, two methods per annotation type, under 100 lines total for all current annotations). The benefit is:

1. Compile-time type safety. `node.GetSyntaxTokens()` returns `IReadOnlyList<SyntaxToken>?`, not `object?`.
2. Discoverability. IntelliSense shows all available annotations on any `MarkdownObject`.
3. Documented contracts. The extension method signatures are the de facto schema for inter-transform communication.
4. Refactoring safety. Renaming an annotation type is a rename refactor, not a string search.

I will also accept Wei's third point about the implicit dependency graph between transforms. The extension methods help, but they do not make the ordering dependencies explicit. I will add a `TransformContract` documentation pattern: each transform class documents which annotations it reads (preconditions) and which it writes (postconditions). This is documentation, not runtime enforcement -- but it makes the ordering dependencies visible in code review.

**What I reject from challenge 1:** Wei says "cost of introducing an IR later is bounded" is optimistic and that it would be an emitter rewrite. This overstates the coupling. The emitter's visitor methods access Markdig node properties like `HeadingBlock.Level`, `FencedCodeBlock.Info`, `ParagraphBlock` child inlines, etc. These are structural properties, not internal implementation details. If we introduced an IR, the IR nodes would expose the same properties (level, info, children) because they represent the same concepts. The visitor method bodies would change types but not logic. I stand by "bounded" -- it is a mechanical refactor, not a redesign. But I acknowledge that "bounded" should be "significant but mechanical" to set expectations honestly.

**Changes to ADR-0005:** Added typed extension method layer as a mandatory mitigation. Added transform contract documentation pattern. Softened "bounded" to "significant but mechanical."

---

### ADR-0009 (Important): YAML Theme DSL -- Debug Tooling and Interpolation

**Verdict: Accept points 1 and 2. Partially accept point 3. Reject point 4 as stated.**

**Point 1 -- `md2 theme resolve` command: Accept.**

Wei is absolutely right that a 4-layer cascade without debugging tools is the CSS specificity problem recreated. I should have included this from the start. The command will be:

```
md2 theme resolve [--preset <name>] [--theme <path>] [--template <path>] [--style <overrides>]
```

Output: a table showing every style property, its resolved value, and which layer it came from. Example:

```
Property                 Value          Source
-----------------------  -------------  --------------------------
heading1.fontSize        24pt           --style override (Layer 4)
heading1.color           #1B3A5C        theme.yaml (Layer 3)
heading1.bold            true           preset:technical (Layer 2)
heading1.spaceBefore     24pt           preset:technical (Layer 2)
body.font                Cambria        template.docx (Layer 1)
...
```

This is not just nice to have -- it is essential for the product context's requirement that "built-in style adjustments must have small blast radius." Users need to see the blast radius.

**Point 2 -- Defer `${...}` interpolation for v1: Accept.**

Wei makes a good argument. YAML anchors/aliases already provide a reference mechanism within a single file. Our `${...}` interpolation adds cross-section references (e.g., `docx.heading1.color` referencing `colors.primary`), which anchors cannot do. So it is not fully redundant.

However, for v1, the complexity cost is real: cycle detection, type coercion (is `${typography.baseFontSize}` a string or a measurement?), undefined variable error messages, and interaction with schema validation. These are all solvable but they are implementation hours spent on a convenience feature, not a capability.

For v1, theme YAML files will use plain literal values. The presets will be authored with literal values (which is fine -- they are authored once and rarely modified). Users writing custom themes will duplicate color values across properties. This is an annoyance, not a blocker. We introduce `${...}` interpolation in a point release once we have real-world usage data on how much duplication actually occurs.

**Point 3 -- Drop YAML themes entirely for v1: Partially reject.**

Wei asks "what user story requires a full YAML theme that presets + CLI overrides + template cannot satisfy?" The answer is: the theme extraction workflow. `md2 theme extract corp.docx -o corp.yaml` produces a YAML theme. If YAML themes do not exist as an input format, theme extraction has no output target. The user cannot round-trip: extract from template, tweak a few values, use the tweaked theme. That round-trip is the central workflow for corporate template adoption.

But Wei is right that this inversion deserves documentation. I will add a section to the ADR explaining why YAML themes are necessary for v1 (theme extraction round-trip) rather than leaving it as an implied assumption.

**Point 4 -- Schema versioning locks us in: Reject as stated.**

The concern is real but the conclusion is wrong. Not versioning the schema is worse than versioning it. If we ship v1 without a version field and then need to make breaking changes, we have no way to detect which schema a file targets. The version field (`meta.version: 1`) costs nothing and buys forward compatibility. The risk is not "we commit to properties forever" -- it is "we need a migration story." And the migration story is simple: unknown properties are ignored (forward compatibility), missing properties get defaults (backward compatibility), and breaking changes bump the version number with a documented migration path.

I will add Wei's suggestion that unknown properties should be ignored (rather than rejected) for forward compatibility. This is a good design principle that was missing from the ADR.

**Changes to ADR-0009:** Added `md2 theme resolve` command. Deferred `${...}` interpolation to post-v1. Added explicit justification for why YAML themes are needed in v1. Changed schema validation to ignore unknown properties.

---

### ADR-0008 (Important): Playwright -- Benchmarks, Mermaid JS Override, Linux Compat

**Verdict: Accept all three points.**

**Point 1 -- Benchmarks:** Wei is right that "Mermaid rendering is slow" is hand-waving. I will add concrete acceptance criteria:

- **Target:** 10 unique Mermaid diagrams rendered in under 15 seconds total (including browser cold-start).
- **Per-diagram budget:** Under 2 seconds per diagram after browser is warm.
- **Benchmark method:** Prototype with 10 representative diagrams (flowchart, sequence, class, state, ER, gantt, pie, mindmap, gitgraph, C4) and measure wall-clock time on both Windows and Linux.

If these targets are not met, investigate: (a) are we launching the browser efficiently? (b) is page-level parallelism working? (c) is the PNG screenshot step the bottleneck?

**Point 2 -- `--mermaid-js <path>` flag:** Accept. Bundling Mermaid JS pins users to our release cycle. A user who encounters a Mermaid rendering bug has no recourse until we release. The `--mermaid-js` flag lets advanced users provide their own Mermaid JS file. This is a low-cost escape hatch.

**Point 3 -- Linux compatibility documentation:** Wei is right and the evidence supports his concern. Playwright's Chromium builds pin to specific glibc versions. Known issues include:
- Playwright 1.50+ requires glibc 2.36, which is not available on Ubuntu 22.04 LTS (glibc 2.35).
- Alpine Linux (musl libc) is not supported at all.
- CentOS 7 and RHEL 7 (glibc 2.17) have been broken since Playwright 1.37.

For a daily-driver CLI, this is a real usability risk. I will add:
- A documented list of supported Linux distributions (those with glibc >= the pinned Playwright version).
- A `md2 doctor` or `md2 check` diagnostic command that verifies Playwright/Chromium compatibility.
- A recommendation to pin to a specific Playwright version in the lock file and not upgrade casually.

**Point on SVG intermediate output:** Wei suggests "consider SVG intermediate output as escape hatch when Playwright unavailable." I will note this but not commit to it for v1. SVG-to-DOCX embedding has its own problems (Word's SVG support is limited and version-dependent). If Playwright is unavailable, Mermaid diagrams are skipped with a warning -- same as how math degrades without Chromium.

**Changes to ADR-0008:** Added benchmarking acceptance criteria, `--mermaid-js` flag, Linux compatibility documentation requirements, and diagnostic command recommendation.

---

### ADR-0007 (Minor): TextMateSharp -- Oniguruma and Shiki Fallback

**Verdict: Accept all modifications.**

Wei caught a genuine error in my ADR. I wrote "MIT-licensed, no native dependencies (pure C# with an Oniguruma regex wrapper)" -- that sentence contradicts itself. TextMateSharp wraps Oniguruma, which is a native binary. "Pure C# with a native wrapper" is not "no native dependencies." I will correct this.

Adding Shiki-via-Playwright as a documented fallback is sensible. If the Oniguruma native binary causes packaging or deployment issues (single-file publish, Alpine Linux, ARM Linux), we can fall back to running Shiki in the same Chromium instance used for Mermaid and math. The trade-off is that syntax highlighting then requires Chromium, but it works.

I will also add a deployment verification step: test single-file publish (`dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true`) and verify that the Oniguruma native binary is correctly bundled.

**Changes to ADR-0007:** Corrected the "no native dependencies" claim. Added Shiki-via-Playwright as documented fallback. Added deployment verification requirement.

---

### ADR-0010 (Minor): IRM Detection -- Distinguish File Types

**Verdict: Accept all modifications.**

Wei is correct that the OLE compound document magic number conflates three different cases:

1. **IRM-protected DOCX** -- encrypted, needs RMS server authentication.
2. **Password-protected DOCX** -- encrypted with a user password. The Open XML SDK cannot open these, but other libraries (e.g., BouncyCastle for .NET) can decrypt with the password.
3. **Legacy `.doc` file** -- not encrypted, just a different format.

Each needs a different error message and guidance. I will update the detection logic:

- Check file extension first (`.doc` vs `.docx`/`.docm`).
- If `.doc`: "This is a legacy Word format. Save as .docx in Word first."
- If `.docx` with OLE header: attempt to determine if password-protected vs. IRM-protected. For v1, the error message can cover both cases with appropriate guidance for each. A `--template-password` flag is a reasonable v2 feature but not v1 scope.
- Make the 50MB size limit configurable via `--max-template-size` with 50MB as default. Document the rationale: style extraction reads only the styles part of the document, but Open XML SDK loads the entire ZIP. 50MB is generous for any legitimate template.

**Changes to ADR-0010:** Added three-way detection logic. Added distinct error messages. Noted `--template-password` as v2 scope. Made size limit configurable.

---

### ADR-0011 (Minor): System.CommandLine -- Spectre.Console.Cli Fallback

**Verdict: Accept with updated context.**

Wei's primary concern was that System.CommandLine has been pre-release for 5+ years. This concern is now resolved: **System.CommandLine 2.0.4 was released as GA on 2026-03-10** (yesterday). The package is no longer pre-release. The long beta period is over.

This materially changes the evaluation. The risks Wei raised ("API surface has changed between previews" and "migration cost if GA introduces breaking changes") are no longer speculative -- the API is now stable and released.

I will still document Spectre.Console.Cli as an alternative that was considered and rejected, with explicit reasoning:
- System.CommandLine is now GA and Microsoft-maintained.
- It provides tab completion natively, which Spectre.Console.Cli does not offer.
- Middleware pipeline is a cleaner model for cross-cutting concerns than Spectre.Console.Cli's interceptor pattern.
- Using Spectre.Console (not .Cli) for output formatting alongside System.CommandLine for parsing is the intended composition. They are complementary, not competing.

Isolating command definitions behind abstractions is good engineering regardless. I will add this as a recommended practice in the ADR.

**Changes to ADR-0011:** Updated to reflect GA status. Added explicit comparison with Spectre.Console.Cli and rationale for rejection. Added command definition isolation recommendation.

---

### Summary of Changes

| ADR | Verdict | Changes Made |
|-----|---------|-------------|
| 0006 | **Accept (Critical)** | Rewrote: MathML intermediate path via KaTeX + MML2OMML.xsl is now primary. Direct converter demoted to future optimization. |
| 0004 | **Partially accept** | Added 5-day prototype gate for table auto-sizing. Named OOXML spec sections. Added GemBox as documented fallback with trigger criteria. |
| 0005 | **Accept** | Added mandatory typed extension methods. Added transform contract documentation pattern. Softened "bounded" cost estimate. |
| 0009 | **Accept (3 of 4)** | Added `md2 theme resolve` command. Deferred `${...}` interpolation. Added v1 justification for YAML themes. Unknown properties ignored for forward compat. |
| 0008 | **Accept** | Added benchmarking criteria (10 diagrams < 15s). Added `--mermaid-js` flag. Added Linux compat documentation and diagnostic command. |
| 0007 | **Accept (Minor)** | Corrected native dependency claim. Added Shiki fallback. Added deployment verification. |
| 0010 | **Accept (Minor)** | Three-way detection (IRM vs. password vs. legacy). Distinct error messages. Configurable size limit. |
| 0011 | **Accept (Minor)** | Updated to GA status. Added explicit Spectre.Console.Cli comparison. Added isolation recommendation. |
