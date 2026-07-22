#!/usr/bin/env bash
#
# REQ-FN-037 (BRD-58) — package the Mac Catalyst build as a distributable .dmg.
#
# Produces a mountable disk image with the standard drag-to-Applications layout, so a user can
# download ONE file from the repo's releases and install without any developer tooling. This
# replaces the BRD-44 flow ("copy the .app out of bin/ and clear quarantine yourself"), which is
# fine for the developer but cannot be asked of someone on a fresh Mac.
#
# NOTE ON SIGNING (REQ-FN-038 / BRD-59): the .app is ad-hoc signed (CodesignKey '-'), NOT
# Developer ID signed and NOT notarized. macOS WILL quarantine this .dmg when it is downloaded, so
# first launch needs the documented one-time trust step (REQ-FN-040 / BRD-61). Closing that
# properly requires a paid Apple Developer account; this script does not pretend otherwise.
#
# Usage: build/package-mac.sh [output-dir]     (default: artifacts/)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${1:-$REPO_ROOT/artifacts}"
APP_NAME="TrSetup"
TFM="net10.0-maccatalyst"
CONFIG="Release"
APP_PATH="$REPO_ROOT/src/TrSetup/bin/$CONFIG/$TFM/$APP_NAME.app"
VOL_NAME="$APP_NAME"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "package-mac.sh: macOS only (Catalyst builds cannot be produced elsewhere)." >&2
  exit 1
fi

if [[ ! -d "$APP_PATH" ]]; then
  echo "package-mac.sh: no .app at $APP_PATH" >&2
  echo "Build it first:  dotnet build src/TrSetup/TrSetup.csproj -f $TFM -c $CONFIG" >&2
  exit 1
fi

# Read the shipped version so the artifact name is traceable to a build.
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP_PATH/Contents/Info.plist" 2>/dev/null || echo "0.0")"
DMG_PATH="$OUT_DIR/${APP_NAME}-${VERSION}-macOS.dmg"

mkdir -p "$OUT_DIR"
rm -f "$DMG_PATH"

# Stage in a temp dir: the .app plus an /Applications symlink is the conventional drag-install UX.
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

echo "Staging $APP_NAME.app ..."
cp -R "$APP_PATH" "$STAGE/$APP_NAME.app"
ln -s /Applications "$STAGE/Applications"

echo "Creating $DMG_PATH ..."
hdiutil create \
  -volname "$VOL_NAME" \
  -srcfolder "$STAGE" \
  -ov \
  -format UDZO \
  "$DMG_PATH" >/dev/null

SIZE="$(du -h "$DMG_PATH" | cut -f1 | tr -d ' ')"
echo
echo "Built: $DMG_PATH  ($SIZE)"
echo
echo "UNSIGNED BUILD (REQ-FN-038 open): this .dmg is ad-hoc signed and not notarized."
echo "On a fresh Mac the user must right-click the app once and choose Open,"
echo "or run:  xattr -dr com.apple.quarantine /Applications/$APP_NAME.app"
