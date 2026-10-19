// Shared helpers for the diagram tools.
import { createHash } from 'node:crypto';
import { readdirSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const DIAGRAMS_DIR = resolve(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'docs', 'architecture', 'diagrams');
export const sha256 = (buf) => createHash('sha256').update(buf).digest('hex');
export const MARKER = /<!-- src-sha256:([0-9a-f]{64}) -->/;
export const list = (ext) =>
  readdirSync(DIAGRAMS_DIR).filter((f) => f.endsWith(ext)).sort().map((f) => join(DIAGRAMS_DIR, f));
