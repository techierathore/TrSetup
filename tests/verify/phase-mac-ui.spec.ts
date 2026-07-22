/**
 * TrSetup — Phase verification: Mac device-host + Mac app-runner UI (black-box).
 *
 * READ-ONLY test suite. SAFETY RULES (do not violate):
 *  - NEVER click any Fix button ([data-testid^="fix-"], SheetFixButton, fix-all-*) — fixes install software.
 *  - NEVER change RolesSelector / AppSelector values — changes persist to the user's settings file.
 *  - Navigation, deep links, reading text, and screenshots ONLY.
 *
 * App under test: Blazor Server board at http://localhost:5999 (baseURL from playwright.config.ts).
 * The board runs a detect sweep on load (probes capped at 5s each) — waits are generous (30s polls).
 *
 * Machine context (seeded settings): Roles = "DeviceHostMac, AppRunnerMac", SelectedApp = "AppStudio".
 */

import { test, expect, Page, Locator } from '@playwright/test';

// Tests are fully independent — each navigates itself. No serial coupling.
test.describe.configure({ mode: 'default' });

const MAC_CHECK_IDS = [
  'mac.xcode-clt',
  'mac.dotnet-maui',
  'mac.node',
  'mac.appium-drivers',
  'mac.appium-launchagent',
  'mac.stable-ip',
  'mac.ios-simulator',
] as const;

const POLL_TIMEOUT = 30_000;

/**
 * Words that mean "still working" — a settled status must not contain these.
 * "Pending" is included since the REQ-UI-001 streaming fix (2026-07-11): every
 * in-scope row must reach a real verdict; a row left "Pending" after the sweep
 * budget is the stuck-row defect this suite exists to catch.
 */
const UNSETTLED = /pending|checking|loading|running|detecting|…|\.\.\./i;

/**
 * Navigate to the board root and wait for it to settle:
 *  - at least one board row is visible,
 *  - every status badge has non-empty, non-"checking" text.
 * Blazor Server + SignalR makes waitForLoadState('networkidle') unreliable,
 * so we poll on locator counts/text instead.
 */
async function waitForBoard(page: Page): Promise<void> {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('[data-testid^="board-row-"]').first()).toBeVisible({
    timeout: POLL_TIMEOUT,
  });

  // Wait for the detect sweep to settle: all rendered status badges non-empty
  // and none of them still say "checking" (or show only a spinner).
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
      { timeout: POLL_TIMEOUT, message: 'board status badges should settle to real verdicts' }
    )
    .toBe('settled');
}

/** Trimmed inner text of a locator. */
async function textOf(locator: Locator): Promise<string> {
  return (await locator.innerText()).trim();
}

/**
 * Assert a row carries real evidence, not just a title + status.
 * Preferred: the evidence element rendered inside the row (.ts-row-ev-side,
 * discovered black-box from the served markup). Fallback: the row's full text
 * must be meaningfully longer than title + status alone.
 * Returns the evidence text found.
 */
async function expectRowEvidence(page: Page, id: string): Promise<string> {
  const row = page.locator(`[data-testid="board-row-${id}"]`);
  const evidence = row.locator('.ts-row-ev-side');

  if ((await evidence.count()) > 0) {
    const evText = await textOf(evidence.first());
    expect(evText, `evidence text for ${id} should be non-empty`).not.toBe('');
    return evText;
  }

  // Fallback: whole-row text must exceed title + status by a real margin.
  const rowText = await textOf(row);
  const titleText = await textOf(page.locator(`[data-testid="row-title-${id}"]`));
  const statusText = await textOf(page.locator(`[data-testid="status-${id}"]`));
  expect(
    rowText.length,
    `row ${id} text should contain evidence beyond title ("${titleText}") + status ("${statusText}")`
  ).toBeGreaterThan(titleText.length + statusText.length + 5);
  return rowText;
}

/** Assert a status badge is settled: visible, non-empty, and not a spinner/"checking". */
async function expectSettledStatus(page: Page, id: string): Promise<string> {
  const status = page.locator(`[data-testid="status-${id}"]`);
  await expect(status, `status badge for ${id} should be visible`).toBeVisible({
    timeout: POLL_TIMEOUT,
  });
  const text = await textOf(status);
  expect(text, `status text for ${id} should be non-empty`).not.toBe('');
  expect(text, `status for ${id} should be a settled verdict, not "${text}"`).not.toMatch(
    UNSETTLED
  );
  // A verdict is a short word (Pass / Fail / Warn / N/A / Pending / Blocked ...), not a sentence.
  expect(text.length, `status for ${id} should be a short verdict word`).toBeLessThanOrEqual(30);
  return text;
}

// ---------------------------------------------------------------------------
// REQ-FN-008 — Mac device-host detects
// ---------------------------------------------------------------------------

test('REQ-FN-008 Mac device-host detects: one row per mac check with real status and evidence', async ({
  page,
}, testInfo) => {
  await waitForBoard(page);

  for (const id of MAC_CHECK_IDS) {
    const row = page.locator(`[data-testid="board-row-${id}"]`);
    await expect(row, `board row for ${id} should be visible`).toBeVisible({
      timeout: POLL_TIMEOUT,
    });

    const statusText = await expectSettledStatus(page, id);
    const evidenceText = await expectRowEvidence(page, id);
    testInfo.annotations.push({
      type: `evidence:${id}`,
      description: `status="${statusText}" evidence="${evidenceText}"`,
    });
  }

  // mac.node: Node v26 is installed on this machine — evidence should carry a
  // version-like string OR the status should be Pass.
  const nodeEvidence = await expectRowEvidence(page, 'mac.node');
  const nodeStatus = await textOf(page.locator('[data-testid="status-mac.node"]'));
  const versionLike = /v?\d+\.\d+(\.\d+)?/;
  expect(
    versionLike.test(nodeEvidence) || /pass/i.test(nodeStatus),
    `mac.node should show a version-like evidence string or Pass (status="${nodeStatus}", evidence="${nodeEvidence}")`
  ).toBe(true);

  // mac.xcode-clt: CLT is installed at /Library/Developer/CommandLineTools — must be Pass.
  const cltStatus = await textOf(page.locator('[data-testid="status-mac.xcode-clt"]'));
  expect(cltStatus, 'mac.xcode-clt should be Pass (CLT installed on this machine)').toMatch(
    /pass/i
  );
});

// ---------------------------------------------------------------------------
// REQ-FN-027 — Mac app-runner aggregation
// ---------------------------------------------------------------------------

test('REQ-FN-027 Mac app-runner aggregation: AppStudio profile rows and Catalyst build row render', async ({
  page,
}) => {
  await waitForBoard(page);

  // The Catalyst build fixer row must be on the board.
  await expect(
    page.locator('[data-testid="board-row-appstudio.maccatalyst-build"]')
  ).toBeVisible({ timeout: POLL_TIMEOUT });

  // At least 8 AppStudio profile rows.
  const appstudioRows = page.locator('[data-testid^="board-row-appstudio."]');
  await expect
    .poll(async () => appstudioRows.count(), {
      timeout: POLL_TIMEOUT,
      message: 'at least 8 appstudio.* board rows should render',
    })
    .toBeGreaterThanOrEqual(8);

  // Each AppStudio row has a settled, non-empty status.
  const count = await appstudioRows.count();
  for (let i = 0; i < count; i++) {
    const row = appstudioRows.nth(i);
    const testId = await row.getAttribute('data-testid');
    const id = testId!.replace('board-row-', '');
    await expectSettledStatus(page, id);
  }
});

// ---------------------------------------------------------------------------
// REQ-UI-001 — board streaming: every row settles to a real verdict (no stuck Pending)
// ---------------------------------------------------------------------------

const STUCK_PRONE_IDS = [
  'mac.appium-launchagent',
  'appstudio.github-packages-feed',
  'appstudio.maccatalyst-build',
] as const;

test('REQ-UI-001 board streaming: all rows settle to Pass/Fail/Warn/NA, none stuck Pending (2 fresh loads)', async ({
  page,
}, testInfo) => {
  // Two independent loads in one test to cover the intermittency of the old defect.
  for (let load = 1; load <= 2; load++) {
    await waitForBoard(page); // waitForBoard itself now rejects "Pending" as unsettled

    const statuses = page.locator('[data-testid^="status-"]');
    const texts = await statuses.allInnerTexts();
    const verdictLike = /^(pass|fail|warn|n\/a)$/i;
    const bad = texts.filter((t) => !verdictLike.test(t.trim()));
    expect(bad, `load #${load}: every status must be a real verdict, got: ${JSON.stringify(bad)}`).toEqual([]);

    // The three historically stuck-Pending rows must each carry a settled verdict.
    for (const id of STUCK_PRONE_IDS) {
      const text = await expectSettledStatus(page, id);
      testInfo.annotations.push({ type: `settled:${id}`, description: `load#${load} status="${text}"` });
    }
  }
});

// ---------------------------------------------------------------------------
// REQ-FN-028 — Catalyst build fixer row (verifiable portion; live build is owner UAT)
// ---------------------------------------------------------------------------

test('REQ-FN-028 Catalyst build fixer sheet: deep link renders title, evidence and fix preview', async ({
  page,
}, testInfo) => {
  // SAFETY: read the sheet only — do NOT click SheetFixButton.
  await page.goto('/check/appstudio.maccatalyst-build', { waitUntil: 'domcontentloaded' });

  const sheet = page.locator('[data-testid="CheckSheet"]');
  await expect(sheet).toBeVisible({ timeout: POLL_TIMEOUT });

  // Title non-empty.
  const title = page.locator('[data-testid="SheetTitle"]');
  await expect(title).toBeVisible({ timeout: POLL_TIMEOUT });
  await expect
    .poll(async () => (await title.innerText()).trim().length, {
      timeout: POLL_TIMEOUT,
      message: 'SheetTitle should have non-empty text',
    })
    .toBeGreaterThan(0);

  // Evidence must SETTLE to a real detect result. A deep link lands on a fresh
  // circuit whose sweep is still running, so the sheet legitimately shows
  // "not yet detected / never detected" first — the sheet must then stream the
  // live result in (REQ-UI-001 streaming fix covers the detail screen too).
  const evidence = page.locator('[data-testid="SheetEvidence"]');
  await expect(evidence).toBeVisible({ timeout: POLL_TIMEOUT });
  await expect
    .poll(async () => (await evidence.innerText()).trim(), {
      timeout: POLL_TIMEOUT,
      message: 'SheetEvidence should settle to a real detect result (not "never detected")',
    })
    .toMatch(/^(?!.*(not yet detected|never detected|pending|checking))(?=.*\S).*$/is);
  const evidenceText = (await evidence.innerText()).trim();
  testInfo.annotations.push({
    type: 'appstudio.maccatalyst-build evidence',
    description: evidenceText,
  });
  console.log(`[REQ-FN-028] SheetEvidence: ${evidenceText}`);

  // Since the gate-detect budget fix (2026-07-11) the evidence must be REAL gate
  // output — red prerequisite ids or the ready/built message — never the row's
  // own probe timeout (the old unbounded sequential prereq sweep).
  expect(
    evidenceText,
    'gate evidence should be real (red prereq ids / ready-to-build / built .app), not a probe timeout'
  ).toMatch(/prerequisites still red|ready to build|catalyst \.app built/i);
  expect(evidenceText.toLowerCase()).not.toContain('probe timed out');

  // Fix preview visible and shows the exact build command (read-only — never clicked).
  const fixPreview = page.locator('[data-testid="SheetFixPreview"]');
  await expect(fixPreview).toBeVisible({ timeout: POLL_TIMEOUT });
  const previewText = (await fixPreview.innerText()).trim();
  expect(previewText).toContain('dotnet build');
  expect(previewText.toLowerCase()).toContain('maccatalyst');
});

// ---------------------------------------------------------------------------
// RENDER-SWEEP (§4a) — Board dashboard
// ---------------------------------------------------------------------------

test('RENDER-SWEEP (§4a) board dashboard: all DevGuide controls, groups, badges and rows render', async ({
  page,
}) => {
  await waitForBoard(page);

  // DevGuide-listed controls.
  for (const controlId of ['RolesSelector', 'AppSelector', 'RecheckAllButton', 'ExportReportButton']) {
    await expect(
      page.locator(`[data-testid="${controlId}"]`),
      `${controlId} should be visible`
    ).toBeVisible({ timeout: POLL_TIMEOUT });
  }

  // At least one group card.
  expect(
    await page.locator('[data-testid^="board-group-"]').count(),
    'at least one board group card should render'
  ).toBeGreaterThanOrEqual(1);

  // Count badges present.
  expect(
    await page.locator('[data-testid^="count-pass-"]').count(),
    'pass-count badges should be present'
  ).toBeGreaterThanOrEqual(1);

  // Total rows > 15, all with non-empty settled status.
  const rows = page.locator('[data-testid^="board-row-"]');
  await expect
    .poll(async () => rows.count(), {
      timeout: POLL_TIMEOUT,
      message: 'board should render more than 15 rows',
    })
    .toBeGreaterThan(15);

  const statuses = page.locator('[data-testid^="status-"]');
  const statusTexts = await statuses.allInnerTexts();
  expect(statusTexts.length, 'every row should render a status badge').toBeGreaterThan(15);
  for (const [i, text] of statusTexts.entries()) {
    expect(text.trim(), `status badge #${i} should be non-empty`).not.toBe('');
  }

  // No sweep error, no empty-board placeholder.
  await expect(page.locator('[data-testid="SweepError"]')).toHaveCount(0);
  await expect(page.locator('[data-testid="BoardEmpty"]')).toHaveCount(0);
});

// ---------------------------------------------------------------------------
// VISUAL-TRUTH (§4b) — screenshots + geometry at desktop and mobile
// ---------------------------------------------------------------------------

test('VISUAL-TRUTH (§4b) desktop 1280x800: full-page screenshot, control geometry, no horizontal overflow', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await waitForBoard(page);

  // Screenshot is written even on pass (explicit call, fullPage).
  await page.screenshot({ path: 'test-results/phase-mac-board-desktop.png', fullPage: true });

  // Key controls must occupy real, in-viewport space.
  const keyControls: Array<[string, Locator]> = [
    ['RolesSelector', page.locator('[data-testid="RolesSelector"]')],
    ['AppSelector', page.locator('[data-testid="AppSelector"]')],
    ['RecheckAllButton', page.locator('[data-testid="RecheckAllButton"]')],
    ['first board row', page.locator('[data-testid^="board-row-"]').first()],
  ];
  for (const [name, locator] of keyControls) {
    const box = await locator.boundingBox();
    expect(box, `${name} should have a bounding box`).not.toBeNull();
    expect(box!.width, `${name} width > 0`).toBeGreaterThan(0);
    expect(box!.height, `${name} height > 0`).toBeGreaterThan(0);
    expect(box!.x, `${name} x >= 0`).toBeGreaterThanOrEqual(0);
    expect(box!.y, `${name} y >= 0`).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width, `${name} fits horizontally in 1280px viewport`).toBeLessThanOrEqual(
      1280
    );
  }

  // No horizontal page overflow at desktop.
  const overflow = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }));
  expect(
    overflow.scrollWidth,
    `no horizontal overflow at desktop (scrollWidth=${overflow.scrollWidth}, innerWidth=${overflow.innerWidth})`
  ).toBeLessThanOrEqual(overflow.innerWidth + 2);

  // Detail sheet screenshot at desktop (deep link, read-only — never touch SheetFixButton).
  await page.goto('/check/appstudio.maccatalyst-build', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('[data-testid="CheckSheet"]')).toBeVisible({ timeout: POLL_TIMEOUT });
  await page.screenshot({ path: 'test-results/phase-mac-sheet-desktop.png', fullPage: true });
});

test('VISUAL-TRUTH (§4b) mobile 390x844: full-page screenshot; overflow recorded, not failed (documented caveat)', async ({
  page,
}, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await waitForBoard(page);

  // KNOWN DOCUMENTED CAVEAT: the status table overflows horizontally at 390px
  // (non-blocking; desktop is the target). Record the numbers, do NOT fail.
  const overflow = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    innerWidth: window.innerWidth,
  }));
  const note = `mobile 390px: scrollWidth=${overflow.scrollWidth}, innerWidth=${overflow.innerWidth}` +
    (overflow.scrollWidth > overflow.innerWidth + 2
      ? ' — horizontal overflow present (known caveat, non-blocking)'
      : ' — no horizontal overflow');
  console.log(`[VISUAL-TRUTH mobile] ${note}`);
  testInfo.annotations.push({ type: 'mobile-overflow', description: note });

  // Screenshot is written even on pass.
  await page.screenshot({ path: 'test-results/phase-mac-board-mobile.png', fullPage: true });
});

// ---------------------------------------------------------------------------
// (render-sweep) console errors — zero console errors / failed asset requests
// ---------------------------------------------------------------------------

/** SignalR / websocket reconnect noise that Blazor Server produces legitimately. */
function isSignalRNoise(url: string): boolean {
  return (
    url.startsWith('ws://') ||
    url.startsWith('wss://') ||
    url.includes('/_blazor') ||
    url.toLowerCase().includes('signalr') ||
    url.includes('negotiate')
  );
}

test('(render-sweep) console errors: zero console errors and zero failed asset requests on board load', async ({
  page,
}) => {
  const consoleErrors: string[] = [];
  const failedRequests: string[] = [];

  page.on('console', (msg) => {
    if (msg.type() !== 'error') return; // severity 'error' only
    const text = msg.text();
    // Allow websocket / SignalR reconnect noise.
    if (isSignalRNoise(text) || /websocket|signalr|circuit/i.test(text)) return;
    consoleErrors.push(text);
  });

  page.on('requestfailed', (request) => {
    const url = request.url();
    if (isSignalRNoise(url)) return;
    failedRequests.push(`${url} — ${request.failure()?.errorText ?? 'unknown failure'}`);
  });

  page.on('response', (response) => {
    const url = response.url();
    if (isSignalRNoise(url)) return;
    if (response.status() >= 400) {
      failedRequests.push(`${url} — HTTP ${response.status()}`);
    }
  });

  await waitForBoard(page);
  // Give late-loading assets and the sweep a moment to surface any errors.
  await page.waitForTimeout(2000);

  expect(consoleErrors, `console errors seen: ${JSON.stringify(consoleErrors, null, 2)}`).toEqual(
    []
  );
  expect(
    failedRequests,
    `failed requests seen: ${JSON.stringify(failedRequests, null, 2)}`
  ).toEqual([]);
});

// ---------------------------------------------------------------------------
// (render-sweep) unresolved icons — no LucideIcon fallback text in the raw DOM
// ---------------------------------------------------------------------------

/**
 * Every route the shell can reach. The sidebar (and therefore every icon in it)
 * renders on all of them, but page-local icons only appear on their own screen —
 * so the guard has to visit each one.
 */
const ICON_SWEEP_ROUTES = ['/', '/fix-run', '/report', '/setup', '/settings'] as const;

/**
 * Regression guard for REQ-NFR-003 (2026-07-21).
 *
 * When LucideIcon cannot resolve a name it does NOT throw and does NOT log a console
 * error — it renders the literal text "Icon not found: <name>". That text sits in an
 * SVG-adjacent fallback node, so it is absent from innerText too, and it silently
 * corrupts the accessible name of whatever control contains it (the Settings nav link
 * read as "Icon not found: sliders Settings" in the XCUITest a11y tree). Six verify
 * runs missed it for exactly that reason.
 *
 * The assertion MUST run against page.content() (raw serialised DOM). Asserting on
 * innerText or on console output would reproduce the original blind spot.
 */
test('(render-sweep) unresolved icons: no "Icon not found" fallback text in the raw DOM on any screen', async ({
  page,
}) => {
  const offenders: string[] = [];

  // Start from the board so the detect sweep has run and status icons are rendered.
  await waitForBoard(page);

  for (const route of ICON_SWEEP_ROUTES) {
    await page.goto(route, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('[data-testid="NavSettings"]')).toBeVisible({
      timeout: POLL_TIMEOUT,
    });
    // Let Blazor finish its first interactive render before serialising the DOM.
    await page.waitForTimeout(1000);

    const html = await page.content();
    for (const match of html.matchAll(/Icon not found:\s*([^<"]*)/g)) {
      offenders.push(`${route} → "${match[0].trim()}"`);
    }
  }

  expect(
    offenders,
    `unresolved LucideIcon names found in raw DOM: ${JSON.stringify(offenders, null, 2)}`
  ).toEqual([]);
});
