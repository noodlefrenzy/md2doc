---
agent-notes: { ctx: "whatsit reference page for Visitor Pattern Variants", deps: [], state: active, last: "whatsit@2026-03-13" }
---

# Visitor Pattern Variants

> **One-sentence summary:** The Visitor pattern separates algorithms from object structures via double dispatch, but has spawned a family of variants — from GoF classic to acyclic to "just use pattern matching" — each trading off extensibility, boilerplate, and coupling differently.

**Canonical reference:** [Refactoring Guru — Visitor](https://refactoring.guru/design-patterns/visitor)
**Expression Problem context:** [Eli Bendersky — The Expression Problem](https://eli.thegreenplace.net/2016/the-expression-problem-and-its-solutions/)

---

## What Problem Does It Solve?

- You have a hierarchy of node types (e.g., an AST) and want to define **new operations** over them without modifying each node class
- Traditional OOP makes adding new types easy but adding new operations hard — the Visitor inverts this trade-off
- This is one face of the **Expression Problem**: extending both types and operations without modifying existing code

## The Variants

### 1. GoF Classic (Double Dispatch)

The textbook version. Each element class implements `Accept(IVisitor)`, and the visitor has a `Visit()` overload per element type. Dispatch is "double" — first dynamic dispatch to the element's Accept, then overload resolution to the correct Visit method.

```
// Element side
void Accept(IVisitor v) => v.Visit(this);

// Visitor side
void Visit(HeadingBlock h) { ... }
void Visit(Paragraph p) { ... }
```

| Pros | Cons |
|------|------|
| Compile-time type safety | Every element must implement Accept |
| Adding new operations = new visitor class | Adding new element types = modify ALL visitors |
| Well-understood, widely documented | Boilerplate-heavy (N×M method signatures) |

**Best for:** Stable type hierarchies with frequently changing operations (compilers, linters, serializers).

### 2. Acyclic Visitor

Introduced by Robert C. Martin. Breaks the cyclic dependency between the visitor and element hierarchies by using separate interfaces per element type.

```
interface IVisitor { }  // marker
interface IHeadingVisitor : IVisitor { void Visit(HeadingBlock h); }
interface IParagraphVisitor : IVisitor { void Visit(Paragraph p); }
```

Each Accept checks at runtime (`is` / `dynamic_cast`) whether the visitor implements the relevant sub-interface.

| Pros | Cons |
|------|------|
| Adding new element types doesn't break existing visitors | Runtime type check per visit (slower) |
| No cyclic compile-time dependency | More interfaces to manage |
| Open for extension in both dimensions | Less IDE discoverability |

**Best for:** Frequently extended hierarchies where the GoF version's "modify all visitors" tax is unacceptable.

**Reference:** [Robert C. Martin — Acyclic Visitor (PDF)](https://condor.depaul.edu/dmumaugh/OOT/Design-Principles/acv.pdf)

### 3. Hierarchical Visitor

Extends the classic visitor with **enter/exit** callbacks and **traversal control**. The visitor receives `EnterHeading()` / `ExitHeading()` pairs, and can return a boolean to skip subtrees.

| Pros | Cons |
|------|------|
| Depth awareness (know parent/child context) | More method signatures (2× per type) |
| Can short-circuit branches | Traversal logic can get complex |
| Natural fit for tree/DOM structures | |

**Best for:** Deep tree structures where traversal depth and branch skipping matter (DOM walkers, XML processors, AST transforms).

**Reference:** [C2 Wiki — Hierarchical Visitor Pattern](https://wiki.c2.com/?HierarchicalVisitorPattern=)

### 4. Internal vs. External Visitor

An orthogonal axis that applies to any variant:

| | Internal | External |
|---|---------|----------|
| **Who controls traversal?** | The data structure | The caller |
| **Analogy** | `forEach` | `Iterator` |
| **Visitor role** | Provides the action only | Drives both traversal and action |
| **Flexibility** | Less (fixed traversal order) | More (caller decides order, depth, skipping) |

GoF classic is typically **internal** — the composite's Accept method recurses into children. External visitors give the caller full control over iteration, which is useful when different operations need different traversal orders.

### 5. Pattern-Matching Dispatch (Modern Variant)

In languages with sum types or exhaustive pattern matching (Rust, C#, Scala, Java 21+), you can replace the entire Accept/Visit ceremony with a switch expression:

```csharp
// C# pattern-matching visitor (what md2doc uses)
IEnumerable<OpenXmlElement> VisitBlock(Block block) => block switch
{
    HeadingBlock h   => VisitHeading(h),
    MdTable t        => VisitTable(t),
    FencedCodeBlock c => VisitFencedCodeBlock(c),
    _                => Enumerable.Empty<OpenXmlElement>()
};
```

| Pros | Cons |
|------|------|
| Zero boilerplate — no Accept methods needed | No compile-time exhaustiveness if hierarchy isn't sealed |
| Works with types you don't control (e.g., Markdig AST) | Adding a new type = update all switch expressions (same as GoF) |
| Idiomatic in modern C#, Rust, Scala | Traversal logic isn't reusable across visitors |
| Graceful fallback via `_` wildcard | |

**Best for:** When you don't own the element types, or when the type hierarchy is stable and you want minimal ceremony.

### 6. Dynamic Visitor (Multiple Dispatch)

In languages with `dynamic` or multi-methods (C# `dynamic`, Common Lisp, Julia), the runtime resolves the correct overload without any Accept infrastructure:

```csharp
void Visit(dynamic element) => VisitImpl(element); // runtime dispatch
void VisitImpl(HeadingBlock h) { ... }
void VisitImpl(Paragraph p) { ... }
```

| Pros | Cons |
|------|------|
| Minimal boilerplate | Runtime cost of dynamic dispatch |
| No Accept methods | No compile-time safety |
| Easy to add types and operations | Debugging is harder |

## Comparison Matrix

| Variant | Add Types | Add Operations | Boilerplate | Performance | Type Safety |
|---------|-----------|---------------|-------------|-------------|-------------|
| GoF Classic | Hard | Easy | High | Fast | Full |
| Acyclic | Easy | Easy | Medium | Slower (casts) | Partial |
| Hierarchical | Hard | Easy | Very High | Fast | Full |
| Pattern-Match | Hard | Easy | **Minimal** | Fast | Partial* |
| Dynamic | Easy | Easy | Minimal | Slower | None |

\* Full exhaustiveness if the hierarchy is sealed/enum-based.

## How md2doc Chose

md2doc uses **pattern-matching dispatch** (variant 5) in `DocxAstVisitor`. The rationale:

- **Markdig owns the AST** — md2doc can't add Accept methods to Markdig's node types
- **Node types are stable** — Markdig's block/inline types don't change often
- **C# pattern matching** gives clean, readable dispatch with a safe `_` fallback
- **Single visitor** — there's only one DOCX emitter, so reusable traversal logic isn't needed yet

This is the pragmatic choice: minimum ceremony for the constraints at hand.

## Honest Assessment

**Strengths**
- The GoF visitor is one of the few patterns that genuinely solves a real problem (operation extensibility over closed hierarchies)
- Modern variants (pattern-matching, acyclic) fix the worst pain points of the original
- Understanding the trade-off space helps you pick the right tool for each situation

**Weaknesses**
- The GoF classic is widely over-applied — if your hierarchy changes often, it's the wrong tool
- None of the variants fully solve the Expression Problem (extending both types and operations)
- The pattern can obscure control flow, especially with recursive visitors

**Verdict:** If your type hierarchy is stable, pattern-matching dispatch is the modern default. Reach for GoF classic or hierarchical only when you need reusable traversal logic across multiple visitor implementations. Reach for acyclic when the hierarchy is genuinely open-ended.

---

*Sources:*
- [Refactoring Guru — Visitor Pattern](https://refactoring.guru/design-patterns/visitor)
- [Refactoring Guru — Visitor and Double Dispatch](https://refactoring.guru/design-patterns/visitor-double-dispatch)
- [Wikipedia — Visitor Pattern](https://en.wikipedia.org/wiki/Visitor_pattern)
- [Robert C. Martin — Acyclic Visitor (PDF)](https://condor.depaul.edu/dmumaugh/OOT/Design-Principles/acv.pdf)
- [C2 Wiki — Hierarchical Visitor Pattern](https://wiki.c2.com/?HierarchicalVisitorPattern=)
- [nipafx — Visitor Pattern Considered Pointless](https://nipafx.dev/java-visitor-pattern-pointless/)
- [Eli Bendersky — The Expression Problem](https://eli.thegreenplace.net/2016/the-expression-problem-and-its-solutions/)
- [Rust Design Patterns — Visitor](https://rust-unofficial.github.io/patterns/patterns/behavioural/visitor.html)
- [Li Haoyi — Zero-Overhead Tree Processing with the Visitor Pattern](https://www.lihaoyi.com/post/ZeroOverheadTreeProcessingwiththeVisitorPattern.html)
- [Java Design Patterns — Acyclic Visitor](https://java-design-patterns.com/patterns/acyclic-visitor/)

*Generated by Whatsit · 2026-03-13*
