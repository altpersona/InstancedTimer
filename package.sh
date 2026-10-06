#!/bin/sh
# Build the Thunderstore package zip: manifest + README + icon + Plugins/DLL at zip root.
# Requires ./build.sh to have been run (uses its Release output).
set -e
cd "$(dirname "$0")"

DLL="SunkenCryptTimer/bin/Release/SunkenCryptTimer.dll"
[ -f "$DLL" ] || { echo "missing $DLL - run ./build.sh first" >&2; exit 1; }
[ -f SunkenCryptTimer/icon.png ] || { echo "missing SunkenCryptTimer/icon.png - run tools/make_icon.py" >&2; exit 1; }

VERSION=$(grep -o '"version_number": *"[^"]*"' SunkenCryptTimer/manifest.json | sed 's/.*"\([0-9.]*\)"/\1/')
STAGE=$(mktemp -d)
OUT="SunkenCryptTimer-v${VERSION}.zip"

mkdir -p "$STAGE/Plugins"
cp "$DLL" "$STAGE/Plugins/SunkenCryptTimer.dll"
cp SunkenCryptTimer/manifest.json "$STAGE/manifest.json"
cp README.md "$STAGE/README.md"
cp SunkenCryptTimer/icon.png "$STAGE/icon.png"
cp CHANGELOG.md "$STAGE/CHANGELOG.md" 2>/dev/null || true

rm -f "$OUT"
(cd "$STAGE" && zip -qr "$OLDPWD/$OUT" .)
rm -rf "$STAGE"
echo "packaged: $OUT"
unzip -l "$OUT"
