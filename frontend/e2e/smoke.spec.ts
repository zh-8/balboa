import { expect, test } from '@playwright/test';

const apiUrl = (process.env['BALBOA_API_URL'] ?? 'http://localhost:5080').replace(/\/+$/, '');

test.describe('post-deploy smoke', () => {
  test('app loads and renders the shell', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Balboa', level: 1 })).toBeVisible();
  });

  test('api is reachable and serves feature flags', async ({ request }) => {
    const response = await request.get(`${apiUrl}/api/features`);
    expect(response.ok()).toBe(true);
    expect(typeof (await response.json())).toBe('object');
  });

  test('app reaches the api from the browser', async ({ page }) => {
    const apiCall = page.waitForResponse((r) => r.url().startsWith(apiUrl) && r.url().includes('/api/'));
    await page.goto('/');
    expect((await apiCall).status()).toBeLessThan(500);
  });
});
