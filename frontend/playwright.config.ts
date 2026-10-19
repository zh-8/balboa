import { defineConfig, devices } from '@playwright/test';

/**
 * Post-deploy smoke tests ONLY. Not part of `npm test` and never in the PR gate.
 *   BALBOA_BASE_URL  frontend under test (default: local dev server)
 *   BALBOA_API_URL   API under test      (default: local backend)
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env['BALBOA_BASE_URL'] ?? 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
