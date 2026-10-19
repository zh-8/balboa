# ADR-0003: Boundary enforcement by three layers

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

A modular monolith is only modular while its boundaries are respected. In a single solution
the compiler makes cross-module calls trivial, and AI agents optimise for "make it compile".
Convention and code review alone will erode the boundaries; they need to fail the build.

## Decision drivers

- Violations must fail automatically, not depend on a reviewer noticing.
- Feedback should be fast: the earlier the layer, the cheaper the failure.
- Defence in depth: no single mechanism should be the only guard.
- Tooling must be maintainable by one person.

## Considered options

1. Convention and code review only.
2. Architecture tests only (ArchUnitNET).
3. Separate repositories or NuGet packages per module.
4. Three layers: MSBuild guard, ArchUnitNET tests, `internal` visibility.

## Decision

Enforce boundaries in three layers, fastest feedback first:

1. **MSBuild guard** in `backend/Directory.Build.targets`. A `ProjectReference` from
   `Modules/X/**` to `Modules/Y/**` fails the build unless the target ends in `.Contracts`.
   `*.Domain` may reference only `SharedKernel`.
2. **ArchUnitNET tests** in `Balboa.ArchitectureTests`. Domain is free of EF Core and
   ASP.NET; Application is free of Infrastructure; no cross-module non-Contracts dependency;
   Contracts contain only records and interfaces; Host touches only `AddXModule` and
   `MapXEndpoints`; each `DbContext` maps only its own entities; handlers are
   `internal sealed`.
3. **Visibility.** Implementation types are `internal`. `InternalsVisibleTo` is granted only
   to a module's own sibling projects and its tests.

## Consequences

### Good

- A violation fails at restore or build time (layer 1) before any test runs.
- Rules the build guard cannot express (type-level dependencies) are caught by layer 2.
- Layer 3 makes the wrong thing hard to even type; IntelliSense does not offer internals.
- Rules are code, so they are versioned, reviewed and documented by the tests themselves.

### Bad

- The custom MSBuild targets are bespoke; new contributors must learn them.
- ArchUnitNET rules can be too loose or too strict, and need maintenance as modules evolve.
- `InternalsVisibleTo` for tests slightly weakens encapsulation.
- Layers overlap on purpose, so a legitimate exception must be granted in more than one place.
- None of this stops reflection or a deliberate bypass by someone editing the rules.

## Revisit trigger

Reopen if the guard produces repeated false positives that agents work around, if ArchUnitNET
becomes unmaintained or incompatible with a new .NET release, or if a module is extracted to
its own deployable (the build guard then becomes a package-boundary concern).
