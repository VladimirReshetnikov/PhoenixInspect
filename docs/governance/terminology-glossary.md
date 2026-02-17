# Terminology Glossary

This glossary establishes a shared vocabulary for the conceptual design phase of the IL interpreter and dump-time evaluation project.

The intent is to reduce ambiguity across product, architecture, integration, and planning documents. Definitions here are design-time working definitions, not implementation commitments.

## How to use this glossary

- Prefer glossary terms in new docs when an entry exists.
- If a document needs a narrower local meaning, explicitly note the deviation and link back to this file.
- Add new entries when recurring terms are introduced in multiple proposals.

## Terms

### Abstract domain

A conservative representation of runtime values used by the interpreter when exact concrete values are unavailable or unsafe to derive from dump data.

Abstract domains enable deterministic reasoning under uncertainty and support explainable outcomes such as "unknown", "partially known", or bounded ranges.

### Budgeted execution

Execution constrained by explicit resource limits (for example instruction count, branch exploration, recursion depth, time slice, or memory usage).

Budget exhaustion is a first-class outcome and must be reported with provenance rather than hidden behind implicit timeouts.

### Call classification

The policy-driven categorization of an invocation target into handling modes such as pure intrinsic, modeled framework call, unsupported dynamic dispatch, or opaque external call.

Classification drives whether execution continues, partially evaluates, or yields a conservative miss reason.

### Deterministic replay

A property of analysis where repeated runs over the same dump inputs, metadata inputs, and configuration produce the same observable evaluation result and explanation.

Determinism applies to both value-level outcomes and provenance/diagnostic artifacts.

### Dump-time evaluation

Evaluation of expressions, statements, or stepping operations against a memory dump rather than a live process.

Because execution is reconstructed from static snapshot artifacts, dump-time evaluation prioritizes bounded simulation, explicit uncertainty, and safety-first fallback behavior.

### Effect lattice

A partially ordered model used to describe side-effect confidence and execution safety (for example no-effect, modeled-effect, unknown-effect).

The lattice informs whether operations can be simulated, summarized, or must terminate evaluation with an explainable miss.

### Explainability

The ability of the system to justify outcomes with human-readable and machine-consumable rationale, including evidence sources, assumptions, and uncertainty boundaries.

Explainability is a core contract, not a debugging add-on.

### Miss reason

A normalized reason code and descriptive payload that explains why exact evaluation or stepping could not continue (for example missing metadata, unsupported opcode, budget exceeded, unresolved generic context).

Miss reasons should be stable enough to support telemetry, testing, and user-facing guidance.

### Provenance

Traceable metadata that links a computed result to its source evidence and transformation path, such as dump memory reads, PE/PDB symbols, policy decisions, and fallback transitions.

Provenance enables auditability and confidence scoring.

### Runtime snapshot

The normalized in-memory representation of dump-derived runtime facts used as analysis input (threads, frames, modules, method bodies, locals, and selected heap state).

It is not a full process recreation; it is a bounded analysis substrate assembled from available evidence.

### Virtual stepping

A dump-backed approximation of debugger Step Into/Over/Out behavior produced through interpreter simulation, debug-map guidance, and conservative stop rules.

Virtual stepping must clearly communicate where behavior diverges from live runtime stepping semantics.

## Open terminology questions

- Should we distinguish "analysis confidence" from "result confidence" as separate first-class fields in public contracts?
- Do we need separate terms for "unsupported by policy" versus "unsupported by capability" to improve user guidance and telemetry quality?
- Should "modeled call" be split into deterministic model versus heuristic model?
