// Native-automation identity bridge (REQ-FN-030 / REQ-NFR-005).
//
// WHY THIS FILE EXISTS
// --------------------
// The Mac Catalyst head hosts this UI in a WKWebView, and XCUITest (Appium mac2) can only
// see whatever WebKit projects into the macOS accessibility tree. It does NOT project the
// element `identifier` for web content: an empirical sweep against the live app proved that
// `id`, `name`, `title`, `aria-roledescription`, `aria-describedby`, `aria-description` and
// `aria-keyshortcuts` ALL leave XCUITest `identifier` (and `title`) empty — even when stacked
// on one element that the same script demonstrably reached. Only two channels survive:
//
//   aria-label       -> XCUITest `label`
//   aria-placeholder -> XCUITest `value` AND `placeholderValue`
//
// We use aria-placeholder. aria-label is the element's ACCESSIBLE NAME — overwriting it with
// a machine id would wreck screen-reader output and re-create exactly the accessible-name
// pollution that the "Icon not found: sliders" bug caused. aria-placeholder, by contrast, is
// only semantically meaningful on textbox/searchbox/combobox roles, so on buttons and links
// it is inert to assistive tech while still reaching XCUITest. That gives native automation a
// stable, intent-named locator channel that is completely independent of visible label text.
//
// WHY MIRROR RATHER THAN HAND-EDIT
// --------------------------------
// The UI already carries 85 `data-testid` attributes that Playwright depends on. Duplicating
// those ids by hand into a second attribute would guarantee drift the moment someone renames
// one. Instead this observer copies data-testid -> aria-placeholder for every element, so the
// two literally cannot disagree, and `data-testid` stays the single source of truth. Adding a
// new testid automatically yields a native locator with no extra work.
//
// Native locator example (Appium mac2):
//   -ios predicate string: value == 'RecheckAllButton'
//
// WHERE THIS IS LOADED (and where it deliberately is NOT)
// -------------------------------------------------------
// - src/TrSetup.Web (headless smoke host): ALWAYS, via _content/TrSetupUI/js/automation-ids.js.
//   That host is test-only and never distributed, and Playwright/verification depends on it.
// - src/TrSetup (the shipping MAUI Catalyst head): DEBUG ONLY. That head has two host documents —
//   wwwroot/index.html (shipping, no mirror) and wwwroot/index.debug.html (mirror) — and
//   MainPage.xaml.cs switches BlazorWebView.HostPage to the latter under #if DEBUG, while
//   TrSetup.csproj keeps both the debug document and this file out of Release output. Reason:
//   aria-placeholder is ARIA-legal only on textbox/searchbox/combobox/spinbutton, so stamping it
//   on buttons/links (exactly what this does) is a spec violation with an untested
//   VoiceOver-announcement risk. Nothing is lost — agent verification drives the DEBUG Catalyst
//   head (.tfcore/core-config.yaml). See the AUTOMATION-ID MIRROR GATE comment in TrSetup.csproj.
// It is a no-op in a plain browser, so the web host is unaffected either way.
(function () {
  "use strict";

  var AUTOMATION_ATTR = "aria-placeholder";

  // Elements where aria-placeholder carries REAL semantics — hijacking it there would change
  // what assistive tech announces (and could shadow a genuine placeholder hint). Native
  // automation falls back to `label` for these; they are containers/inputs, not the action
  // controls REQ-FN-030 needs to drive.
  var SKIP = "input, textarea, [role='textbox'], [role='searchbox'], [role='combobox'], [role='spinbutton']";

  // Interactive roles that WebKit projects as their own XCUITest node.
  var INTERACTIVE = "a, button, [role='button'], [role='link'], [role='menuitem'], [role='tab'], [role='checkbox']";

  function stamp(aEl, aId) {
    if (aEl && !aEl.matches(SKIP) && aEl.getAttribute(AUTOMATION_ATTR) !== aId) {
      aEl.setAttribute(AUTOMATION_ATTR, aId);
    }
  }

  function apply() {
    var vNodes = document.querySelectorAll("[data-testid]");
    for (var vIdx = 0; vIdx < vNodes.length; vIdx++) {
      var vEl = vNodes[vIdx];
      var vId = vEl.getAttribute("data-testid");
      if (!vId) {
        continue;
      }

      // Guarded write: MutationObserver also fires on attribute changes, so an unconditional
      // setAttribute would loop forever.
      stamp(vEl, vId);

      // The sidebar nav puts data-testid on a <span> INSIDE the <a> (e.g. NavBoard). WebKit
      // collapses that span into the link's accessible node, so stamping the span alone is
      // invisible to XCUITest — the id has to ride on the element that actually becomes a
      // node. Walk up to the nearest interactive ancestor, but only when that ancestor owns
      // exactly one testid, otherwise which id wins would depend on DOM order.
      var vHost = vEl.parentElement && vEl.parentElement.closest(INTERACTIVE);
      if (vHost && !vHost.hasAttribute("data-testid") &&
          vHost.querySelectorAll("[data-testid]").length === 1) {
        stamp(vHost, vId);
      }
    }
  }

  function start() {
    apply();
    // Blazor re-renders swap DOM nodes constantly (board rows stream in as checks complete),
    // so a one-shot pass would only ever cover the first paint.
    new MutationObserver(apply).observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["data-testid"]
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", start);
  } else {
    start();
  }
})();
