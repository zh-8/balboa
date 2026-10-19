#!/usr/bin/env node
// Backend coverage gate (ADR-0014). Zero dependencies.
//
//   node check.mjs [--min 85] [--expect-from <dir>] <cobertura.xml>...
//
// Reads Cobertura reports produced by Microsoft.Testing.Extensions.CodeCoverage and exits 1
// when line coverage of any gated assembly, or of all of them together, is below the minimum.
// With --expect-from, every *.Domain / *.Application project found under <dir> must appear in
// the reports; an assembly no test touched is never loaded and would otherwise vanish from the
// report and silently pass, so a missing one counts as 0%.
// Only assemblies named *.Domain or *.Application are counted; Infrastructure, Host,
// Contracts, TestKit and generated code are ignored. Reports are merged per source line (a
// line counts as covered if any report hit it).
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const args = process.argv.slice(2);
let min = 85;
const files = [];
let expectFrom;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--min') min = Number(args[++i]);
  else if (args[i] === '--expect-from') expectFrom = args[++i];
  else files.push(args[i]);
}
if (files.length === 0 || Number.isNaN(min)) {
  console.error('usage: node check.mjs [--min 85] <cobertura.xml>...');
  process.exit(2);
}

const GATED = /\.(Domain|Application)$/;
const byAssembly = new Map(); // name -> Map(file:line -> hits)

for (const file of files) {
  const xml = readFileSync(file, 'utf8');
  for (const m of xml.matchAll(/<package\b([^>]*?)(?:\/>|>([\s\S]*?)<\/package>)/g)) {
    const name = /\bname="([^"]*)"/.exec(m[1])?.[1] ?? '';
    if (!GATED.test(name)) continue;
    const lines = byAssembly.get(name) ?? new Map();
    byAssembly.set(name, lines);
    for (const cls of (m[2] ?? '').matchAll(/<class\b([^>]*)>([\s\S]*?)<\/class>/g)) {
      const filename = /\bfilename="([^"]*)"/.exec(cls[1])?.[1] ?? '';
      for (const l of cls[2].matchAll(/<line\b[^>]*?\bnumber="(\d+)"[^>]*?\bhits="(\d+)"/g)) {
        const key = `${filename}:${l[1]}`;
        lines.set(key, Math.max(lines.get(key) ?? 0, Number(l[2])));
      }
    }
  }
}

const GATED_DIR = /\.(Domain|Application)$/;
function* projects(dir) {
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    if (!e.isDirectory() || ['bin', 'obj', 'node_modules'].includes(e.name)) continue;
    const full = join(dir, e.name);
    if (GATED_DIR.test(e.name) && existsSync(join(full, `${e.name}.csproj`))) yield e.name;
    else yield* projects(full);
  }
}
const missing = [];
if (expectFrom) {
  for (const name of projects(expectFrom)) {
    if (!byAssembly.has(name)) {
      byAssembly.set(name, new Map());
      missing.push(name);
    }
  }
}

if (byAssembly.size === 0) {
  console.error('coverage gate: no *.Domain or *.Application assemblies found in the reports');
  process.exit(1);
}

const pct = (c, t) => (t === 0 ? 0 : (100 * c) / t);
let total = 0;
let covered = 0;
const failing = [];
for (const [name, lines] of [...byAssembly].sort()) {
  const t = lines.size;
  const c = [...lines.values()].filter((h) => h > 0).length;
  total += t;
  covered += c;
  console.log(`  ${name.padEnd(48)} ${pct(c, t).toFixed(1).padStart(6)}%  (${c}/${t} lines)`);
  if (missing.includes(name)) {
    console.log(`    (${name} is absent from the coverage reports: no test executed it)`);
    failing.push(`${name} not covered at all`);
  } else if (pct(c, t) < min) failing.push(`${name} ${pct(c, t).toFixed(1)}%`);
}
const overall = pct(covered, total);
console.log(`Domain+Application line coverage: ${overall.toFixed(1)}% (${covered}/${total}); gate >= ${min}%`);
if (overall < min || failing.length > 0) {
  for (const f of failing) console.error(`  below gate: ${f}`);
  console.error(`coverage gate FAILED: every Domain/Application assembly must be >= ${min}%`);
  process.exit(1);
}
console.log('coverage gate passed');
