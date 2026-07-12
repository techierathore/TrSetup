import { test, expect, Page } from '@playwright/test';

// verify-phase §4a (render) + §4b (visual-truth) for REQ-UI-006 (BRD-56) — the /settings screen.
// Settings are seeded (role + AppStudio + MacIp) so /settings renders directly without first-run.

const WIDTHS = [
  { name: 'desktop', width: 1280, height: 800 },
  { name: 'mobile', width: 390, height: 844 },
];

// A rendered (non-zero) interactive control pushed off the horizontal viewport is a visual defect.
async function visualTruth(page: Page, label: string): Promise<string[]> {
  const fails: string[] = [];
  const boxes = await page.$$eval(
    'button, a[href], select, input, [role="button"], [role="checkbox"], [data-testid^="role-card-"], [data-testid^="profile-req-"]',
    (els) =>
      els
        .filter((e) => (e as HTMLElement).offsetParent !== null)
        .slice(0, 120)
        .map((e) => {
          const r = e.getBoundingClientRect();
          return { id: e.getAttribute('data-testid') || e.tagName.toLowerCase(), x: r.x, w: r.width, h: r.height };
        }),
  );
  const vw = page.viewportSize()!.width;
  for (const b of boxes) {
    if (b.w > 0 && b.h > 0 && (b.x < -4 || b.x > vw + 4))
      fails.push(`${label}: '${b.id}' off-viewport (x=${b.x.toFixed(0)}, vw=${vw})`);
  }
  return fails;
}

test('REQ-UI-006 settings screen renders all regions + reachable from nav', async ({ page }) => {
  // reachable from the shell nav
  await page.goto('/');
  await page.waitForSelector('[data-testid="NavSettings"]', { timeout: 30_000 });
  await page.click('[data-testid="NavSettings"]');
  await page.waitForURL('**/settings', { timeout: 15_000 });

  // §4a render gate: every region present
  await page.waitForSelector('[data-testid^="role-card-"]', { timeout: 15_000 });
  expect(await page.locator('[data-testid^="role-card-"]').count(), 'role cards render').toBeGreaterThanOrEqual(4);
  await expect(page.locator('[data-testid="NativeDevSwitch"]')).toBeVisible();
  await expect(page.locator('[data-testid="SettingsAppSelect"]')).toBeVisible();
  await expect(page.locator('[data-testid="endpoint-MacIp"]')).toBeVisible();
  await expect(page.locator('[data-testid="SettingsSaveButton"]')).toBeVisible();
  await expect(page.locator('[data-testid="SettingsCancelButton"]')).toBeVisible();

  // settings-file path shown (parity with the withdrawn `trsetup config`)
  const pathText = (await page.locator('[data-testid="SettingsFilePath"]').innerText().catch(() => '')).toLowerCase();
  expect(pathText, 'settings-file path shown').toContain('settings.json');

  // pre-filled endpoint value proves TrSetupSettings.Endpoints loaded from the settings file
  // (exact edit→persist is asserted in the second test, so this stays decoupled from that mutation)
  const ip = await page.locator('#endpoint-input-MacIp').inputValue();
  expect(ip, 'MacIp pre-filled from settings (valid IP)').toMatch(/^\d{1,3}(\.\d{1,3}){3}$/);

  // profile-details pane lists AppStudio requirement rows + a source badge (built-in vs override)
  const reqRows = await page.locator('[data-testid^="profile-req-"]').count();
  expect(reqRows, 'AppStudio profile requirement rows render').toBeGreaterThan(0);
  const srcText = (await page.locator('[data-testid^="profile-src-"]').first().innerText().catch(() => '')).toLowerCase();
  expect(srcText.length, 'profile source badge renders text').toBeGreaterThan(0);

  // §4b visual gate. Desktop is the real target for this WSL/Mac dev tool: strict there (no control
  // off-viewport, no body overflow). At mobile the TrBlazeUI sidebar shell keeps a wide content area
  // (the same documented, owner-accepted non-blocking caveat as REQ-UI-001) — screenshot + confirm the
  // data still renders, but do not impose a stricter mobile bar than the rest of the suite meets.
  for (const vp of WIDTHS) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.waitForTimeout(300);
    await page.screenshot({ path: `test-results/settings-${vp.name}.png`, fullPage: true });
    if (vp.name === 'desktop') {
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow, `no horizontal body overflow @${vp.name}`).toBeLessThanOrEqual(2);
      const fails = await visualTruth(page, `settings@${vp.name}`);
      expect(fails, fails.join('\n')).toEqual([]);
    } else {
      // data still renders at mobile (reachable via scroll)
      expect(await page.locator('[data-testid^="role-card-"]').count()).toBeGreaterThanOrEqual(4);
      expect(await page.locator('[data-testid="SettingsProfileTable"]').isVisible()).toBeTruthy();
    }
  }
});

test('REQ-UI-006 endpoint validation gates Save; edit persists', async ({ page }) => {
  await page.goto('/settings');
  await page.waitForSelector('#endpoint-input-MacIp', { timeout: 30_000 });
  const ipInput = page.locator('#endpoint-input-MacIp');

  // Commit a value with real keystrokes — the TrBlazeUI Input is a controlled component bound on
  // `oninput`, so a programmatic .fill() gets re-synced back to the bound state; pressSequentially
  // types character-by-character so each oninput commits through ValueChanged.
  // Blazor Server round-trips each keystroke over SignalR, so the DOM value can momentarily run ahead
  // of the bound state. Rather than trust the DOM, retry the whole type-and-observe until the *effect*
  // (validation state) settles — Playwright's toPass() re-runs the block. Fast typing dropping chars
  // is a test-harness artifact, not a validation bug (the diagnostic proved typing → error → disabled).
  const type = async (value: string) => {
    await ipInput.click();
    await ipInput.press('Control+a');
    await ipInput.press('Delete');
    await page.waitForTimeout(120);
    await ipInput.pressSequentially(value, { delay: 140 });
    await page.waitForTimeout(300);
  };

  // invalid address → error shown + Save disabled (Button signals disabled via `aria-disabled`)
  await expect(async () => {
    await type('x!');
    await expect(page.locator('[data-testid="endpoint-error-MacIp"]')).toBeVisible({ timeout: 2000 });
    await expect(page.locator('[data-testid="SettingsSaveButton"]')).toHaveAttribute('aria-disabled', 'true', { timeout: 2000 });
  }).toPass({ timeout: 30_000 });

  // valid address → field holds it, no error, Save enabled
  await expect(async () => {
    await type('192.168.1.77');
    await expect(ipInput).toHaveValue('192.168.1.77', { timeout: 2000 });
    await expect(page.locator('[data-testid="endpoint-error-MacIp"]')).toHaveCount(0, { timeout: 2000 });
    await expect(page.locator('[data-testid="SettingsSaveButton"]')).toHaveAttribute('aria-disabled', 'false', { timeout: 2000 });
  }).toPass({ timeout: 30_000 });

  await page.click('[data-testid="SettingsSaveButton"]');
  await page.waitForURL('**/', { timeout: 15_000 }).catch(() => {});

  // reopen /settings — value persisted (proves JsonSettingsStore save + reload)
  await page.goto('/settings');
  await page.waitForSelector('#endpoint-input-MacIp', { timeout: 15_000 });
  const persisted = await page.locator('#endpoint-input-MacIp').inputValue();
  expect(persisted, 'edited MacIp persisted across reload').toBe('192.168.1.77');
});
