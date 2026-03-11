---
agent-notes: { ctx: "ADR for YAML theme DSL with 4-layer cascade", deps: [docs/architecture.md], state: active, last: "archie@2026-03-11", key: ["${} interpolation deferred to post-v1", "md2 theme resolve command is mandatory"] }
---

# ADR-0009: YAML Theme DSL with 4-Layer Cascade Resolution

## Status

Proposed

## Context

md2's styling system must support multiple configuration sources with clear precedence. Users need to:
- Use built-in presets with zero configuration
- Create custom themes via YAML files
- Use existing DOCX templates as style sources
- Make surgical overrides from the CLI

These sources must compose predictably. When a style property is defined in multiple sources, the resolution must be deterministic and the user must understand what won.

**Options evaluated for theme format:**

1. **YAML** -- Human-readable, supports comments, widely known. Variable interpolation via `${...}` syntax. Good tooling support (schema validation via JSON Schema). YamlDotNet is the standard .NET YAML library.

2. **TOML** -- Simpler than YAML, no indentation sensitivity. Less expressive for nested structures. Less common in the .NET ecosystem.

3. **JSON** -- No comments. Verbose for deeply nested style definitions. Not suitable for a file humans edit by hand.

4. **Custom DSL** -- Maximum expressiveness. Learning curve. Tooling cost (parser, validator, editor support).

**Options evaluated for cascade model:**

A. **Last-writer-wins flat merge.** Simple but lossy. A theme that defines `heading1.fontSize` would obliterate all other `heading1` properties from the preset.

B. **Deep merge with layer priority.** Each property is resolved independently by walking layers from highest to lowest priority. A CLI override of `heading1.fontSize` does not affect `heading1.color` from the theme YAML. This is CSS-like specificity without the complexity.

## Decision

Use **YAML** as the theme file format with **YamlDotNet** for parsing.

Use **deep merge with 4-layer priority** for cascade resolution:

```
Layer 4 (highest):  CLI --style overrides
Layer 3:            theme.yaml (--theme flag)
Layer 2:            Preset defaults (--preset flag, or "default" if none specified)
Layer 1 (lowest):   template.docx styles (--template flag, extracted at runtime)
```

**Resolution rules:**

1. For each style property (e.g., `heading1.fontSize`), walk from Layer 4 down to Layer 1. The first layer that defines the property wins.
2. If no layer defines a required property, use the hardcoded fallback from the "default" preset (which is always Layer 2 if no explicit preset is specified).
3. **Gap warnings.** If the AST contains an element (e.g., Heading 4) and no layer defines a style for it, emit a CLI warning: `Warning: No style defined for Heading4. Using default preset.`
4. **Variable interpolation -- deferred to post-v1.** ~~YAML theme files support `${...}` references.~~ For v1, all values are plain literals. Variable interpolation (`${colors.primary}`) will be introduced in a point release once we have real-world usage data on how much value duplication actually occurs in practice. This avoids v1 implementation cost for cycle detection, type coercion, undefined variable handling, and interaction with schema validation. YAML anchors/aliases can serve as a partial workaround for intra-section references in the meantime.
5. **Validation.** Theme YAML is validated against a JSON Schema embedded in the assembly. Invalid properties produce clear error messages with line numbers. **Unknown properties are ignored** (not rejected) for forward compatibility -- a theme YAML written for a newer md2 version should load without error in an older version, with the new properties silently discarded.

**Debug command (added after Wei debate):** `md2 theme resolve` is a mandatory CLI command for debugging the cascade:

```
md2 theme resolve [--preset <name>] [--theme <path>] [--template <path>] [--style <overrides>]
```

Output: a table showing every style property, its resolved value, and which layer it came from:

```
Property                 Value          Source
-----------------------  -------------  --------------------------
heading1.fontSize        24pt           --style override (Layer 4)
heading1.color           #1B3A5C        theme.yaml (Layer 3)
heading1.bold            true           preset:technical (Layer 2)
heading1.spaceBefore     24pt           preset:technical (Layer 2)
body.font                Cambria        template.docx (Layer 1)
```

This is not optional. A 4-layer cascade without a debugging tool is the CSS specificity problem recreated.

**Theme extraction (reverse direction):**

`md2 theme extract <template.docx> -o theme.yaml` reads the template's style definitions and produces a YAML theme file. This YAML can then be edited and used as Layer 3 input. Extraction is best-effort: styles that use theme colors or complex inheritance may not extract perfectly. The YAML output includes comments explaining what was inferred vs. what was explicit.

**Why YAML themes are needed for v1 (not just presets + CLI overrides + template):**

Wei raised the inversion: "What if you dropped YAML themes for v1 and only shipped presets + CLI overrides + template?" The answer is that the theme extraction workflow requires YAML as an output format. `md2 theme extract corp.docx -o corp.yaml` produces YAML. If YAML themes are not an input format, there is no round-trip: extract from template, tweak a few values, feed the tweaked theme back. That round-trip is the central workflow for corporate template adoption. Without it, users who want to customize an extracted theme must either modify the original DOCX template (defeating the purpose of extraction) or use a long chain of `--style` overrides (unergonomic for 10+ properties).

**Schema:**

The theme YAML schema is versioned (`meta.version: 1`). Future schema changes use a migration path. The schema is designed for forward compatibility: unknown properties are ignored (a theme written for md2 v1.3 loads in v1.0 without error). The schema supports:
- `meta` section (name, description, version)
- `typography` section (shared font definitions)
- `colors` section (shared color palette with named tokens)
- `docx` section (format-specific style definitions)
- `pptx` section (format-specific, v2)

## Consequences

### Positive

- **Predictable cascade.** Users can reason about which layer a property comes from. `--verbose` mode shows the resolution trace.
- **Composable.** A theme YAML only needs to define overrides. Everything else falls through to the preset.
- **Template interop.** Users can start from an existing DOCX template and incrementally override specific styles via YAML without recreating everything.
- **Variable interpolation (post-v1).** Changing a primary color in one place will update all styles that reference it once `${...}` interpolation is implemented.
- **Schema validation.** Typos and invalid values are caught before rendering, not silently ignored.
- **YAML is familiar.** No learning curve for the format itself.

### Negative

- **YAML parsing pitfalls.** The Norway problem (`NO` becomes `false`), unquoted strings that look like numbers, indentation sensitivity. Mitigated by strict YamlDotNet deserialization settings and schema validation.
- **Value duplication in v1.** Without `${...}` interpolation, theme YAML files will have duplicated color values and font names across properties. This is an annoyance for theme authors, not a blocker. YAML anchors/aliases provide a partial workaround for intra-section references. Full interpolation is planned for a post-v1 release.
- **4-layer cascade is powerful but potentially confusing.** A user with a template + theme + preset + CLI overrides might struggle to understand why a specific style was applied. Mitigated by the mandatory `md2 theme resolve` debug command that shows which layer each property came from.
- **Template style extraction is lossy.** DOCX styles have features (style inheritance, latent styles, linked styles) that do not map cleanly to flat YAML properties. Some information is lost during extraction.

### Neutral

- The `pptx:` section of the theme schema is defined now but not used until v2. This is intentional -- it ensures the schema is stable when PPTX support arrives.
- Preset YAML files are bundled as embedded resources in the `Md2.Themes` assembly. They can also be exported for inspection (`md2 theme list --show-path`).
