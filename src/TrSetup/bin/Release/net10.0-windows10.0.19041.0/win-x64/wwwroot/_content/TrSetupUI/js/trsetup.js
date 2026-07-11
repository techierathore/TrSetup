// TrSetupUI JS interop module (loaded on demand via JS isolation — no script tag needed).

/**
 * Copies text to the system clipboard (report copy-markdown, fix-preview copy).
 * @param {string} aText The text to place on the clipboard.
 * @returns {Promise<void>} Resolves when the clipboard write completed.
 */
export function copyText(aText) {
  if (navigator.clipboard && window.isSecureContext) {
    return navigator.clipboard.writeText(aText);
  }
  // Fallback for non-secure contexts (plain-http LAN hosts).
  const vArea = document.createElement("textarea");
  vArea.value = aText;
  vArea.style.position = "fixed";
  vArea.style.opacity = "0";
  document.body.appendChild(vArea);
  vArea.focus();
  vArea.select();
  document.execCommand("copy");
  document.body.removeChild(vArea);
  return Promise.resolve();
}
