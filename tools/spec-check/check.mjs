#!/usr/bin/env node
// Spec traceability checker (ADR-0013). Zero runtime dependencies.
//
//   node check.mjs [--pr-body <file>] [--root <repo>]    (or PR_BODY env var = body text)
//
// Rules:
//   FAIL  criterion in an Accepted/Implemented spec has no tagged test
//   FAIL  a tag references a criterion that no spec defines
//   WARN  spec is Draft (criteria not enforced)
//   FAIL  PR body supplied but has no `Spec:` line (skipped when no body is supplied)
import { readdirSync, readFileSync, existsSync, statSync } from 'node:fs';
import { join, relative, resolve, dirname, extname } from 'node:path';
import { fileURLToPath } from 'node:url';

const IGNORED_DIRS = new Set(['node_modules', 'bin', 'obj', 'dist', '.angular', '.git']);
const AC = /AC-\d{4}-\d{2}/g;
const ENFORCED = new Set(['accepted', 'implemented']);

const args = process.argv.slice(2);
const opt = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};
const verbose = args.includes('--verbose');
const root = resolve(opt('--root') ?? join(dirname(fileURLToPath(import.meta.url)), '..', '..'));
const rel = (p) => relative(root, p).split('\\').join('/');

function* walk(dir) {
  if (!existsSync(dir)) return;
  for (const name of readdirSync(dir)) {
    if (IGNORED_DIRS.has(name)) continue;
    const full = join(dir, name);
    const st = statSync(full);
    if (st.isDirectory()) yield* walk(full);
    else yield full;
  }
}

const lineOf = (text, index) => text.slice(0, index).split('\n').length;
// A tag inside a comment (e.g. the doc comment on the attribute itself) is not a test.
const inComment = (text, index) => /^\s*(\/\/|\/?\*)/.test(text.slice(text.lastIndexOf('\n', index - 1) + 1, index + 1));

// ---- 1. specs -------------------------------------------------------------
const criteria = new Map(); // id -> { spec, status, file, line }
const specs = [];
const specsDir = join(root, 'docs', 'specs');
for (const file of walk(specsDir)) {
  if (!/SPEC-[^/\\]*[/\\]spec\.md$/.test(file)) continue;
  const text = readFileSync(file, 'utf8');
  const fm = /^---\r?\n([\s\S]*?)\r?\n---/.exec(text);
  const field = (k) => new RegExp(`^${k}:\\s*(.+?)\\s*$`, 'm').exec(fm?.[1] ?? '')?.[1];
  const status = (field('status') ?? 'unknown').replace(/^["']|["']$/g, '');
  const id = field('id') ?? rel(file);
  specs.push({ id, status, file: rel(file) });
  for (const m of text.matchAll(AC)) {
    if (!criteria.has(m[0])) criteria.set(m[0], { spec: id, status, file: rel(file), line: lineOf(text, m.index) });
  }
}

// ---- 2. tags --------------------------------------------------------------
const tags = []; // { id, file, line, kind }
const backendDir = join(root, 'backend');
const frontendDir = join(root, 'frontend');
for (const file of walk(backendDir)) {
  if (extname(file) !== '.cs') continue;
  const text = readFileSync(file, 'utf8');
  for (const m of text.matchAll(/[[,]\s*Spec(?:Attribute)?\s*\(([^)]*)\)/g)) {
    if (inComment(text, m.index)) continue;
    for (const id of m[1].match(AC) ?? []) tags.push({ id, file: rel(file), line: lineOf(text, m.index), kind: 'dotnet' });
  }
}
for (const file of walk(frontendDir)) {
  if (extname(file) !== '.ts') continue;
  const text = readFileSync(file, 'utf8');
  for (const m of text.matchAll(/\[(AC-\d{4}-\d{2})\]/g)) {
    if (inComment(text, m.index)) continue;
    tags.push({ id: m[1], file: rel(file), line: lineOf(text, m.index), kind: 'ts' });
  }
}

// ---- 3. evaluate ----------------------------------------------------------
const errors = [];
const warnings = [];
const covering = (id) => tags.filter((t) => t.id === id);

for (const s of specs) {
  const st = s.status.toLowerCase();
  if (st === 'draft') warnings.push(`${s.file}: ${s.id} is Draft; its criteria are not enforced until Accepted.`);
  else if (!ENFORCED.has(st) && st !== 'superseded')
    warnings.push(`${s.file}: ${s.id} has unrecognised status "${s.status}" (expected Draft, Accepted, Implemented or Superseded).`);
}
for (const [id, c] of criteria) {
  if (!ENFORCED.has(c.status.toLowerCase()) || covering(id).length) continue;
  errors.push(
    `${c.file}:${c.line}: ${id} (${c.spec}, ${c.status}) has no tagged test. ` +
      `Add [Spec("${id}")] to a backend test or put [${id}] in a frontend test title.`,
  );
}
for (const t of tags) {
  if (criteria.has(t.id)) continue;
  errors.push(
    `${t.file}:${t.line}: tag ${t.id} references a criterion that no spec defines. ` +
      `Fix the typo, or restore the criterion in docs/specs; IDs are never reused.`,
  );
}

const prBody = opt('--pr-body') ? readFileSync(resolve(opt('--pr-body')), 'utf8') : process.env.PR_BODY;
const prChecked = prBody !== undefined;
if (prChecked && !/^\s*Spec:\s*\S+/m.test(prBody)) {
  errors.push('PR body: no "Spec:" line. Add e.g. "Spec: SPEC-0001" (or "Spec: none - <reason>" for trivial changes).');
}

// ---- 4. report ------------------------------------------------------------
console.log(`spec-check: ${specs.length} spec(s), ${criteria.size} criteria, ${tags.length} tag(s)`);
for (const d of ['backend', 'frontend']) if (!existsSync(join(root, d))) console.log(`note: ${d}/ does not exist yet; its tests cannot cover anything.`);
console.log('');
const rows = [...criteria].sort(([a], [b]) => a.localeCompare(b)).map(([id, c]) => {
  const tests = covering(id).map((t) => `${t.file}:${t.line}`);
  const shown = verbose ? tests : tests.slice(0, 2);
  const more = tests.length - shown.length;
  return [id, c.status, tests.length ? `${tests.length} test(s): ${shown.join(', ')}${more ? `, +${more} more (--verbose)` : ''}` : '(none)'];
});
if (rows.length) {
  const w = [0, 1].map((i) => Math.max(...rows.map((r) => r[i].length), ['Criterion', 'Status'][i].length));
  const pad = (s, n) => s.padEnd(n);
  console.log(`${pad('Criterion', w[0])}  ${pad('Status', w[1])}  Covering tests`);
  console.log(`${'-'.repeat(w[0])}  ${'-'.repeat(w[1])}  ${'-'.repeat(15)}`);
  for (const r of rows) console.log(`${pad(r[0], w[0])}  ${pad(r[1], w[1])}  ${r[2]}`);
  console.log('');
}
if (!prChecked) console.log('note: no PR body supplied (--pr-body / PR_BODY); "Spec:" line check skipped.');
for (const w of warnings) console.log(`WARN  ${w}`);
for (const e of errors) console.error(`FAIL  ${e}`);
console.log(errors.length ? `\nspec-check FAILED with ${errors.length} problem(s).` : '\nspec-check passed.');
process.exit(errors.length ? 1 : 0);
