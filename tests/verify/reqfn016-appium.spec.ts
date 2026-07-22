/**
 * REQ-FN-016 smoke — the mac.appium-drivers board row after the Appium fixer fix.
 * READ-ONLY: never clicks a Fix button; navigation, reading and screenshots only.
 */
import { test, expect } from '@playwright/test';

const POLL = 60_000;
const UNSETTLED = /pending|checking|loading|running|detecting|…|\.\.\./i;

test('REQ-FN-016 mac.appium-drivers settles to Pass with accurate evidence', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('[data-testid^="board-row-"]').first()).toBeVisible({ timeout: POLL });

  const vStatus = page.locator('[data-testid="status-mac.appium-drivers"]');
  const vRow = page.locator('[data-testid="board-row-mac.appium-drivers"]');
  await expect(vRow).toBeVisible({ timeout: POLL });

  await expect.poll(async () => (await vStatus.innerText()).trim(), { timeout: POLL })
    .not.toMatch(UNSETTLED);

  const vStatusText = (await vStatus.innerText()).trim();
  const vRowText = (await vRow.innerText()).replace(/\s+/g, ' ').trim();
  console.log('APPIUM STATUS >>> ' + vStatusText);
  console.log('APPIUM ROW    >>> ' + vRowText);

  const vNode = page.locator('[data-testid="status-mac.node"]');
  console.log('NODE STATUS   >>> ' + (await vNode.innerText()).trim());
  console.log('NODE ROW      >>> ' + (await page.locator('[data-testid="board-row-mac.node"]').innerText()).replace(/\s+/g, ' ').trim());

  await page.screenshot({ path: 'test-results/reqfn016-board.png', fullPage: true });
  await vRow.screenshot({ path: 'test-results/reqfn016-appium-row.png' });

  // Visual-truth: the row is on-screen with real size.
  const vBox = await vRow.boundingBox();
  expect(vBox!.width).toBeGreaterThan(100);
  expect(vBox!.height).toBeGreaterThan(10);

  // Render-truth: evidence is actually present, not a bare title+status.
  expect(vRowText.length).toBeGreaterThan(vStatusText.length + 20);

  expect(vStatusText).toMatch(/pass/i);
  expect(vRowText).toContain('3.5.2');
  expect(vRowText).toContain('xcuitest@11.17.7');
  expect(vRowText).toContain('mac2@4.0.4');
  expect(vRowText).toContain('.trsetup/tools/appium');
});
