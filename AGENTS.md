# Working Instructions

## First-step reading
- Read `README.md` and `docs/README.md` before making any changes.

## Current project phase
- The project is currently in **early development stages**.
- The main deliverables are documentation artifacts in `docs/*.md`.
- The source code is in `src`.
- Snapshots of some relevant libraries are under `lib`. They are provided as a reference to help with our design process, and should be treated as immutable. Do not add direct references to them from `src`, use corresponding NuGet packages. The set of libraries is not exhaustive; other can be used as needed.

## Documentation ownership and expectations
- Continuously expand and refine the documentation set.
- Keep documents well-structured, consistent, and easy to navigate.
- Treat documentation direction as an owned responsibility: make decisions proactively, and revise earlier decisions when better options emerge.
- Do not wait for fully detailed task breakdowns; take initiative and move the design forward.

## API documentation requirement
- All **public types and public methods** in source code must include detailed XML documentation comments.
- XML docs should explain intent, parameters/returns, and caveats so design rationale remains discoverable in code.
