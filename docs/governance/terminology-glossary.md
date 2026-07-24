# Terminology Glossary

> **Lifecycle:** Draft · **Roadmap:** Supporting

This glossary establishes a shared vocabulary for the early development phase of the IL interpreter and dump-time evaluation project.

The intent is to reduce ambiguity across product, architecture, integration, and planning documents. Definitions here are design-time working definitions, not implementation commitments.

## How to use this glossary

- Prefer glossary terms in new docs when an entry exists.
- If a document needs a narrower local meaning, explicitly note the deviation and link back to this file.
- Add new entries when recurring terms are introduced in multiple proposals.

## Terms

### Abstract domain

A conservative representation of runtime values used by the interpreter when exact concrete values are unavailable or unsupported to derive from dump data.

Abstract domains enable deterministic reasoning under uncertainty and support explainable outcomes such as "unknown", "partially known", or bounded ranges.

### Budgeted execution

Execution constrained by explicit replay-stable resource limits (for example instruction count, branch exploration, call depth, traversal count, allocation units, or memory usage). Host cancellation or wall-clock deadlines are separate responsiveness mechanisms.

Budget exhaustion is a first-class outcome and must be reported with provenance rather than hidden behind implicit timeouts.

### Call classification

The policy-driven categorization of an invocation target into handling modes such as pure intrinsic, modeled framework call, unsupported dynamic dispatch, or opaque external call.

Classification drives whether execution continues, partially evaluates, or yields a conservative miss reason.

### Completion status

The operational outcome of a request, kept separate from what its value means. Working categories include completed, blocked, budget-exhausted, cancelled, decision-needed, and failed.

Completion is not a confidence score: a completed counterfactual execution can still rely on models or assumptions.

### Completeness

How much of the requested answer was produced: complete, partial, or none. Completeness is separate from completion and evidence quality; for example, a request can complete normally with a partial answer because some dump pages were unavailable.

### Counterfactual execution

Interpreter execution that starts from dump-derived, user-provided, or assumed state and answers what code **would** compute under explicit policies and models.

Counterfactual execution is not historical replay and cannot establish why the original process reached the captured state.

### Deterministic replay

A property of analysis where repeated runs over the same dump inputs, metadata inputs, and configuration produce the same observable evaluation result and explanation.

Determinism applies to both value-level outcomes and provenance/diagnostic artifacts.

For a virtual session, replay means reproducing the tool's command transcript; it does not mean replaying the original process's historical execution.

### Dump-time evaluation

Evaluation of expressions, statements, or stepping operations against a memory dump rather than a live process.

Because execution is reconstructed from static snapshot artifacts, dump-time evaluation prioritizes bounded simulation, explicit uncertainty, and conservative fallback behavior.

### Evidence status

The quality of the evidence supporting a result, independent of request completion or semantic mode. Working categories are exact, partial, unavailable, conflicting, and invalid.

Evidence status must carry provenance and miss reasons. It must not be collapsed into a single confidence badge inside engine contracts.

### Effect lattice

A partially ordered model used to describe side-effect confidence and execution limits (for example no-effect, modeled-effect, unknown-effect).

The lattice informs whether operations can be simulated, summarized, or must terminate evaluation with an explainable miss.

### Explainability

The ability of the system to justify outcomes with human-readable and machine-consumable rationale, including evidence sources, assumptions, and uncertainty boundaries.

Explainability is a core contract, not a debugging add-on.

### Miss reason

A normalized reason code and descriptive payload that explains why exact evaluation or stepping could not continue (for example missing metadata, unsupported opcode, budget exceeded, unresolved generic context).

Miss reasons should be stable enough to support diagnostic output, testing, and user-facing guidance.

### Semantic mode

The kind of claim an evaluation result makes:

- **Observation**: a fact decoded directly from snapshot evidence.
- **Derived query**: a deterministic calculation over observed facts without executing user IL.
- **Counterfactual execution**: interpreted execution from recovered or assumed state.
- **Abstract analysis**: may/must reasoning over a set of possible states.

Every host-facing result should identify its semantic mode. A UI result indicator may summarize mode, completion, completeness, evidence, effects, and provenance, but must not replace those axes.

### Provenance

Traceable metadata that links a computed result to its source evidence and transformation path, such as dump memory reads, PE/PDB symbols, policy decisions, and fallback transitions.

Provenance enables auditability and confidence scoring.

### Runtime snapshot

The normalized in-memory representation of dump-derived runtime facts used as analysis input (threads, frames, modules, method bodies, locals, and selected heap state).

It is not a full process recreation; it is a bounded analysis substrate assembled from available evidence.

### Virtual stepping

A counterfactual, dump-backed approximation of debugger Step Into/Over/Out behavior produced through interpreter simulation, debug-map guidance, and conservative stop rules.

Virtual stepping explores what code would do from a selected snapshot-derived state. It must clearly communicate assumptions and divergence from live-runtime behavior and must never imply causal or historical replay.

## Open terminology questions

- Do we need separate terms for "unsupported by policy" versus "unsupported by capability" to improve user guidance and diagnostic output quality?
- Should "modeled call" be split into deterministic model versus heuristic model?
