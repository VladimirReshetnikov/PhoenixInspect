# Working Instructions

## First-step reading
- Read `README.md` and `docs/README.md` before making any changes.

## Current project phase
- The project is currently in **early development stages**, working toward a first public preview.
- The primary progress signal is executable behavior: source in `src`, tests in `tests`, and the preview demo target
  in `samples`. Documentation in `docs/*.md` is a first-class deliverable alongside it, kept just ahead of and
  consistent with that evidence — never ahead of what the code can actually do.
- `README.md` opens with the preview: what PhoenixInspect answers today and how to run the demo. Everything below
  that heading is the design and milestone record. Keep both accurate.
- Snapshots of some relevant libraries are under `lib`. They are provided as a reference to help with our design process, and should be treated as immutable. Do not add direct references to them from `src`, use corresponding NuGet packages. The set of libraries is not exhaustive; other can be used as needed.

## Preview claims
- Anything the preview documentation says the product answers must be backed by a passing test over a real dump.
- `eng/Invoke-PreviewDemo.ps1` must keep working. The expressions in `eng/demo-session.pi` are asserted by
  `PreviewDemoIntegrationTests`, so changing one without the other fails the fast lane by design.

## Documentation ownership and expectations
- Continuously expand and refine the documentation set.
- Keep documents well-structured, consistent, and easy to navigate.
- Treat documentation direction as an owned responsibility: make decisions proactively, and revise earlier decisions when better options emerge.
- Do not wait for fully detailed task breakdowns; take initiative and move the design forward.

## API documentation requirement
- All **public types and public methods** in source code must include detailed XML documentation comments.
- XML docs should explain intent, parameters/returns, and caveats so design rationale remains discoverable in code.
