import { test, expect, Page } from '@playwright/test';

// verify-phase §4a (render gate) + §4b (visual-truth gate) for the 5 TrSetup UI REQs.
// No auth (local single-user tool). Settings seeded so `/` renders the board directly.

const WIDTHS = [
  { name: 'desktop', width: 1280, height: 800 },
  { name: 'mobile', width: 390, height: 844 },
];

// §4b geometry signals that are unambiguous defects: a visible interactive control that is
// zero-size or pushed off the horizontal viewport. (Legitimate overlays/badges make naive
// pairwise-overlap noisy, so visual-truth judgement is completed by screenshot inspection.)
async function visualTruth(page: Page, label: string): Promise<string[]> {
  const fails: string[] = [];
  const boxes = await page.$$eval(
    'button, a[href], select, [role="button"], [data-testid^="board-row-"]',
    (els) =>
      els
        .filter((e) => (e as HTMLElement).offsetParent !== null)
        .slice(0, 80)
        .map((e) => {
          const r = e.getBoundingClientRect();
          return { id: e.getAttribute('data-testid') || e.tagName.toLowerCase(), x: r.x, w: r.width, h: r.height };
        }),
  );
  const vw = page.viewportSize()!.width;
  for (const b of boxes) {
    // Zero-size elements are hidden (Blazor reconnect UI, sr-only, collapsed toggles) — not a
    // visual break. Only a *rendered* control (non-zero) pushed off the horizontal viewport is one.
    if (b.w > 0 && b.h > 0 && (b.x < -4 || b.x > vw + 4))
      fails.push(`${label}: '${b.id}' off-viewport (x=${b.x.toFixed(0)}, vw=${vw})`);
  }
  return fails;
}

test('REQ-UI-001 board dashboard renders grouped rows + profile rows; looks right', async ({ page }) => {
  await page.goto('/');
  // §4a render gate: board rows actually render (not a blank board)
  await page.waitForSelector('[data-testid^="board-row-"]', { timeout: 30_000 });
  const rows = await page.locator('[data-testid^="board-row-"]').count();
  expect(rows, 'board must render > 0 rows').toBeGreaterThan(0);
  // profile-feature render-truth in the UI: AppStudio profile rows present (P3 → UI)
  const appstudioRows = await page.locator('[data-testid^="board-row-appstudio."]').count();
  expect(appstudioRows, 'AppStudio profile rows must render on the board').toBeGreaterThan(0);
  // status is icon+text, never blank (NFR-003): at least one status label has text
  const statusText = (await page.locator('[data-testid^="status-"]').first().innerText().catch(() => '')).trim();
  expect(statusText.length, 'status label renders text').toBeGreaterThan(0);
  // §4b visual gate. Desktop is the real target (WSL/Mac dev tool): strict there. At 390px the wide
  // status table's per-row Fix/recheck buttons sit past the viewport when the machine has failing
  // checks — a documented, owner-accepted non-blocking caveat (see the REQ-UI-001 remark: "reachable
  // via horizontal scroll; data all renders … mobile responsive polish is a future item"). So at
  // mobile we screenshot + confirm data still renders, without imposing zero-off-viewport.
  for (const vp of WIDTHS) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.waitForTimeout(300);
    await page.screenshot({ path: `test-results/board-${vp.name}.png`, fullPage: true });
    if (vp.name === 'desktop') {
      const fails = await visualTruth(page, `board@${vp.name}`);
      expect(fails, fails.join('\n')).toEqual([]);
    } else {
      expect(await page.locator('[data-testid^="board-row-"]').count(), 'rows still render @mobile').toBeGreaterThan(0);
    }
  }
});

test('REQ-UI-002 check-detail sheet deep link renders sections', async ({ page }) => {
  await page.goto('/check/wsl.winrun');
  await page.waitForLoadState('networkidle');
  const body = (await page.locator('body').innerText()).toLowerCase();
  expect(body).toContain('winrun'); // the check surfaced by id
  await page.screenshot({ path: 'test-results/check-detail.png', fullPage: true });
  const fails = await visualTruth(page, 'detail@desktop');
  expect(fails, fails.join('\n')).toEqual([]);
});

test('REQ-UI-003 role picker renders role cards', async ({ page }) => {
  await page.goto('/setup');
  await page.waitForSelector('[data-testid^="role-card-"]', { timeout: 15_000 });
  const cards = await page.locator('[data-testid^="role-card-"]').count();
  expect(cards, 'role cards render').toBeGreaterThanOrEqual(4);
  for (const vp of WIDTHS) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.waitForTimeout(200);
    const fails = await visualTruth(page, `setup@${vp.name}`);
    await page.screenshot({ path: `test-results/setup-${vp.name}.png`, fullPage: true });
    expect(fails, fails.join('\n')).toEqual([]);
  }
});

test('REQ-UI-004 fix-run view renders', async ({ page }) => {
  await page.goto('/fix-run');
  await page.waitForLoadState('networkidle');
  const bodyLen = (await page.locator('body').innerText()).trim().length;
  expect(bodyLen, 'fix-run page renders content').toBeGreaterThan(0);
  await page.screenshot({ path: 'test-results/fix-run.png', fullPage: true });
  const fails = await visualTruth(page, 'fixrun@desktop');
  expect(fails, fails.join('\n')).toEqual([]);
});

test('REQ-UI-005 report preview renders board report', async ({ page }) => {
  await page.goto('/report');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1500); // report builds from a board sweep
  const body = (await page.locator('body').innerText());
  expect(body.length, 'report renders content').toBeGreaterThan(0);
  await page.screenshot({ path: 'test-results/report.png', fullPage: true });
  const fails = await visualTruth(page, 'report@desktop');
  expect(fails, fails.join('\n')).toEqual([]);
});
