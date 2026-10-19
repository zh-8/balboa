# ADR-0015: Excalidraw for diagrams, with source-hash freshness markers

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Architecture and infrastructure diagrams go stale unless something checks them. Text-based
diagram languages (Mermaid, PlantUML) diff well but lay out poorly for architecture
pictures. Binary exports (PNG) cannot be reviewed. Rendering Excalidraw in CI needs a
headless browser, which is heavy and non-deterministic across versions and fonts.

## Decision drivers

- Diagrams are editable by a human and by an agent (JSON source).
- The picture embedded in docs must be reviewable in a PR.
- A stale diagram must be detectable in CI.
- CI must stay fast and deterministic.

## Considered options

1. Mermaid in Markdown.
2. draw.io or Lucidchart files.
3. Excalidraw source plus exported SVG, re-rendered and compared in CI.
4. Excalidraw source plus exported SVG, with an embedded source-hash marker.

## Decision

Use **Excalidraw**. Commit both the `.excalidraw` source and the exported `.svg` under
`docs/architecture/diagrams/`, and embed the SVG in Markdown. `make diagrams` exports the
SVGs and writes a `<!-- src-sha256:... -->` marker containing the SHA-256 of the source into
each SVG. `make diagrams-check` recomputes the hash of each source and compares it to the
marker in the SVG; a mismatch or missing SVG fails CI.

CI does **not** re-render and pixel-compare, because that needs a browser and is sensitive to
fonts and renderer versions.

## Consequences

### Good

- Fast, deterministic check with no browser in CI.
- Hand-drawn style is easy to produce and edit, and SVG renders on GitHub.
- Editing the source without re-exporting fails the build, so pictures cannot silently lag.

### Bad

- The hash proves the SVG was exported from the current source, not that the export is
  correct; someone could hand-edit the SVG and keep the marker.
- Nothing checks that the diagram matches the actual code or infrastructure; freshness is
  about source and export only. Content accuracy still depends on review.
- Excalidraw JSON diffs are noisy in review; the SVG is the reviewable artifact.
- Export tooling in `tools/diagrams` is ours to maintain.

## Revisit trigger

Reopen if the export tool becomes unmaintainable, if diagrams are repeatedly found to be
inaccurate despite passing the check (consider generating from code, for example Structurizr
or architecture-test output), or if headless rendering in CI becomes cheap and stable.
