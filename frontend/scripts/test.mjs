// Runs `ng test` once (no watch mode). Accepts Vitest's `--run` flag, which the root Makefile
// passes (`npm run test -- --run`) but `ng test` does not know; every other argument is forwarded.
import { spawnSync } from 'node:child_process';

const args = process.argv.slice(2).filter((a) => a !== '--run');
const result = spawnSync('npx', ['ng', 'test', '--no-watch', ...args], {
  stdio: 'inherit',
  shell: process.platform === 'win32',
});
process.exit(result.status ?? 1);
