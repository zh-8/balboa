#!/usr/bin/env node
// Render every docs/architecture/diagrams/*.excalidraw to a sibling .svg with a source-hash marker.
import { readFileSync, writeFileSync } from 'node:fs';
import { list, sha256 } from './lib.mjs';
import { renderSvg } from './render.mjs';

const sources = list('.excalidraw');
if (!sources.length) { console.error('No .excalidraw files found.'); process.exit(1); }
for (const src of sources) {
  const bytes = readFileSync(src);
  let scene;
  try { scene = JSON.parse(bytes.toString('utf8')); } catch (e) { console.error(`${src}: invalid JSON (${e.message})`); process.exit(1); }
  const out = src.replace(/\.excalidraw$/, '.svg');
  writeFileSync(out, renderSvg(scene, sha256(bytes)));
  console.log(`exported ${out.split('/').pop()}`);
}
