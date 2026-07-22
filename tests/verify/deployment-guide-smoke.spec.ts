// Smoke for docs/TrSetup-DeploymentGuide.md §"Verifying a successful deployment".
// Drives the SAME steps the guide tells a deployed user to perform, so the guide cannot
// document a verification recipe that does not actually work. Canonical context #3
// (Device host (Mac) + App runner (Mac), AppStudio) per UsageGuide §8 — no invented user.
import { test, expect } from '@playwright/test';

test('deployment verification recipe: board loads, rows settle to real statuses, none stuck Pending', async ({ page }) => {
  await page.goto('/');

  // Guide step 1: the board renders (not the role picker), with grouped check rows.
  await page.waitForSelector('[data-testid^="board-row-"]', { timeout: 30_000 });
  const rowCount = await page.locator('[data-testid^="board-row-"]').count();
  expect(rowCount, 'board renders rows').toBeGreaterThan(5);

  // Guide step 2: profile rows for the selected application are present.
  const appRows = await page.locator('[data-testid^="board-row-appstudio."]').count();
  expect(appRows, 'AppStudio profile rows render').toBeGreaterThan(0);

  // Guide step 3: statuses stream to REAL verdicts — no row left Pending / never detected.
  await expect.poll(async () => {
    const t = await page.locator('main.ts-content').innerText();
    return /Pending|not yet detected|never detected/i.test(t);
  }, { timeout: 90_000, message: 'every row must settle off Pending' }).toBe(false);

  const statuses = await page.locator('[data-testid^="status-"]').allInnerTexts();
  console.log('row count:', rowCount, '| distinct statuses:', [...new Set(statuses.map(s => s.trim()))].join(', '));
  console.log('--- BOARD ---\n' + (await page.locator('main.ts-content').innerText()).slice(0, 5000));

  await page.screenshot({ path: 'test-results/deployguide-board-desktop.png', fullPage: true });

  // Guide step 4 (first-run configuration): Settings exposes the App Manager endpoint override
  // and the self-signed-certificate opt-in that a multi-machine deployment needs.
  await page.goto('/settings');
  await page.waitForLoadState('networkidle');
  const settingsText = await page.locator('body').innerText();
  expect(settingsText).toMatch(/App Manager/i);
  expect(settingsText).toMatch(/self-signed/i);
  console.log('--- SETTINGS ---\n' + settingsText.slice(0, 3500));
  await page.screenshot({ path: 'test-results/deployguide-settings-desktop.png', fullPage: true });

  // "Looks right", not merely "contains data": no horizontal overflow at the desktop target.
  const overflow = await page.evaluate(() => document.body.scrollWidth - document.body.clientWidth);
  console.log('settings body horizontal overflow:', overflow);
  expect(overflow).toBeLessThanOrEqual(1);
});
