#!/usr/bin/env node
// Fail if any exported SVG is missing, orphaned or stale relative to its .excalidraw source.
import { readFileSync, existsSync } from 'node:fs';
import { basename } from 'node:path';
import { list, sha256, MARKER } from './lib.mjs';

const problems = [];
const sources = list('.excalidraw');
const svgs = list('.svg');
for (const src of sources) {
  const svg = src.replace(/\.excalidraw$/, '.svg');
  const name = basename(src, '.excalidraw');
  if (!existsSync(svg)) { problems.push(`${name}.svg is missing for ${name}.excalidraw.`); continue; }
  const marker = MARKER.exec(readFileSync(svg, 'utf8'));
  if (!marker) { problems.push(`${name}.svg has no src-sha256 marker.`); continue; }
  if (marker[1] !== sha256(readFileSync(src))) problems.push(`${name}.svg is stale: ${name}.excalidraw changed since the last export.`);
}
for (const svg of svgs) {
  const name = basename(svg, '.svg');
  if (!sources.some((s) => basename(s, '.excalidraw') === name)) problems.push(`${name}.svg has no ${name}.excalidraw source.`);
}
if (problems.length) {
  for (const p of problems) console.error(`FAIL  ${p}`);
  console.error('\nRun `make diagrams` and commit the regenerated SVGs.');
  process.exit(1);
}
console.log(`diagrams-check: ${sources.length} diagram(s) up to date.`);
