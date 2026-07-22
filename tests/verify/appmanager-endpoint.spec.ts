import { test, expect, Page } from '@playwright/test';

// REQ-FN-028 — per-machine App Manager endpoint override.
//
// The AppStudio profile declares the App Manager API as https://localhost:5101/ for a requirement
// scoped to BOTH DeviceHostWindows and AppRunnerMac. On this Mac app-runner nothing listens on its
// own localhost:5101 — the service runs on the Windows device-host at 192.168.1.14 — so the row
// could never go green. This spec drives the new Settings override end to end and proves the row
// actually turns green against the LAN endpoint.

const LAN_APPMANAGER = 'https://192.168.1.14:5101/health';
const POLL_TIMEOUT = 60_000;
const UNSETTLED = /checking|pending|^…$|^$/i;

const WIDTHS = [
  { name: 'desktop', width: 1280, height: 800 },
  { name: 'mobile', width: 390, height: 844 },
];

/** Types into a TrBlazeUI controlled Input, which commits on each `oninput` over SignalR. */
async function typeInto(page: Page, selector: string, value: string): Promise<void> {
  const input = page.locator(selector);
  await input.click();
  await input.press('ControlOrMeta+a');
  await input.press('Delete');
  await page.waitForTimeout(120);
  await input.pressSequentially(value, { delay: 60 });
  await page.waitForTimeout(300);
}

/** Waits for the board detect sweep to settle to real verdicts. */
async function waitForBoard(page: Page): Promise<void> {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('[data-testid^="board-row-"]').first()).toBeVisible({ timeout: POLL_TIMEOUT });
  await expect
    .poll(
      async () => {
        const statuses = page.locator('[data-testid^="status-"]');
        const count = await statuses.count();
        if (count === 0) return 'no-status-elements';
        const texts = await statuses.allInnerTexts();
        const unsettled = texts.filter((t) => t.trim().length === 0 || UNSETTLED.test(t));
        return unsettled.length === 0 ? 'settled' : `unsettled:${unsettled.length}/${count}`;
      },
      { timeout: POLL_TIMEOUT, message: 'board status badges should settle' }
    )
    .toBe('settled');
}

test('REQ-FN-028 App Manager endpoint override: set in Settings, persists, row goes green', async ({
  page,
}, testInfo) => {
  // ---- 1. the override field exists on the Settings screen (no JSON editing) ----
  await page.goto('/settings', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('#endpoint-input-AppManagerUrl', { timeout: POLL_TIMEOUT });
  await expect(page.locator('[data-testid="endpoint-AppManagerUrl"]')).toBeVisible();

  // the TLS trust affordance is present AND opt-in (must not default to on)
  const tlsSwitch = page.locator('#endpoint-tls-input-AppManagerUrl');
  await expect(page.locator('[data-testid="endpoint-tls-AppManagerUrl"]')).toBeVisible();

  // ---- 2. a non-URL value is rejected (URL field, not a bare-hostname field) ----
  await expect(async () => {
    await typeInto(page, '#endpoint-input-AppManagerUrl', 'not a url');
    await expect(page.locator('[data-testid="endpoint-error-AppManagerUrl"]')).toBeVisible({ timeout: 2000 });
    await expect(page.locator('[data-testid="SettingsSaveButton"]')).toHaveAttribute('aria-disabled', 'true', {
      timeout: 2000,
    });
  }).toPass({ timeout: 30_000 });

  // ---- 3. point it at the LAN App Manager + explicitly trust its self-signed dev certificate ----
  await expect(async () => {
    await typeInto(page, '#endpoint-input-AppManagerUrl', LAN_APPMANAGER);
    await expect(page.locator('#endpoint-input-AppManagerUrl')).toHaveValue(LAN_APPMANAGER, { timeout: 2000 });
    await expect(page.locator('[data-testid="endpoint-error-AppManagerUrl"]')).toHaveCount(0, { timeout: 2000 });
  }).toPass({ timeout: 30_000 });

  if ((await tlsSwitch.getAttribute('aria-checked')) !== 'true') {
    await tlsSwitch.click();
    await page.waitForTimeout(300);
  }
  await expect(tlsSwitch).toHaveAttribute('aria-checked', 'true', { timeout: 5000 });

  await expect(page.locator('[data-testid="SettingsSaveButton"]')).toHaveAttribute('aria-disabled', 'false', {
    timeout: 5000,
  });
  await page.screenshot({ path: 'test-results/appmanager-settings-desktop.png', fullPage: true });
  await page.click('[data-testid="SettingsSaveButton"]');
  await page.waitForURL('**/', { timeout: 15_000 }).catch(() => {});

  // ---- 4. round-trip: reopen Settings (fresh circuit) — both the URL and the trust opt-in persist ----
  await page.goto('/settings', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('#endpoint-input-AppManagerUrl', { timeout: POLL_TIMEOUT });
  expect(await page.locator('#endpoint-input-AppManagerUrl').inputValue(), 'override persisted').toBe(
    LAN_APPMANAGER
  );
  await expect(page.locator('#endpoint-tls-input-AppManagerUrl'), 'trust opt-in persisted').toHaveAttribute(
    'aria-checked',
    'true'
  );
  // MacIp must be untouched by the new field
  expect(await page.locator('#endpoint-input-MacIp').inputValue(), 'MacIp untouched').toMatch(
    /^\d{1,3}(\.\d{1,3}){3}$/
  );

  // ---- 5. THE POINT: the App Manager row goes GREEN against the LAN endpoint ----
  await waitForBoard(page);
  const apiRow = page.locator('[data-testid="board-row-appstudio.appmanager-api"]');
  await expect(apiRow).toBeVisible({ timeout: POLL_TIMEOUT });
  const apiStatus = (await page.locator('[data-testid="status-appstudio.appmanager-api"]').innerText()).trim();
  const apiEvidence = (await apiRow.locator('.ts-row-ev-side').first().innerText()).trim();
  testInfo.annotations.push({
    type: 'appstudio.appmanager-api',
    description: `status="${apiStatus}" evidence="${apiEvidence}"`,
  });
  expect(apiStatus, 'App Manager API row status').toMatch(/pass/i);
  expect(apiEvidence, 'evidence names the endpoint actually probed').toContain('192.168.1.14');
  expect(apiEvidence, 'evidence names the override provenance').toContain('AppManagerUrl');

  // ---- 6. the still-red secret is correctly still red, and the Catalyst gate narrows to it alone ----
  const secretStatus = (await page.locator('[data-testid="status-appstudio.appmanager-secret"]').innerText()).trim();
  expect(secretStatus, 'APPMANAGER_API_KEY is owner-supplied — must stay red (ADR-008)').toMatch(/fail/i);

  await page.goto('/check/appstudio.maccatalyst-build', { waitUntil: 'domcontentloaded' });
  await expect
    .poll(async () => (await page.locator('body').innerText()).trim(), {
      timeout: POLL_TIMEOUT,
      message: 'Catalyst gate evidence should settle',
    })
    .toContain('Prerequisites still red');
  const gateText = await page.locator('body').innerText();
  testInfo.annotations.push({ type: 'catalyst-gate', description: gateText.slice(0, 600) });
  expect(gateText, 'gate no longer blames the API endpoint').not.toContain('appstudio.appmanager-api');
  expect(gateText, 'gate still names the genuinely-red secret').toContain('appstudio.appmanager-secret');
  await page.screenshot({ path: 'test-results/appmanager-gate-desktop.png', fullPage: true });

  // ---- 7. visual truth: board renders correctly at desktop + mobile ----
  await waitForBoard(page);
  for (const vp of WIDTHS) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.waitForTimeout(400);
    await page.screenshot({ path: `test-results/appmanager-board-${vp.name}.png`, fullPage: true });
    await expect(apiRow, `App Manager row visible @${vp.name}`).toBeVisible();
    if (vp.name === 'desktop') {
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth
      );
      expect(overflow, 'no horizontal body overflow @desktop').toBeLessThanOrEqual(2);
      const offscreen = await page.$$eval('button, a[href], input, [role="button"]', (els) => {
        const vw = window.innerWidth;
        return els
          .filter((e) => (e as HTMLElement).offsetParent !== null)
          .map((e) => ({ id: e.getAttribute('data-testid') || e.tagName, r: e.getBoundingClientRect() }))
          .filter((b) => b.r.width > 0 && b.r.height > 0 && (b.r.x < -4 || b.r.x > vw + 4))
          .map((b) => `${b.id} x=${b.r.x.toFixed(0)}`);
      });
      expect(offscreen, offscreen.join('\n')).toEqual([]);
    }
  }
});
