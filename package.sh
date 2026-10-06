#!/bin/sh
# Build the Thunderstore package zip: manifest + README + icon + Plugins/DLL at zip root.
# Requires ./build.sh to have been run (uses its Release output).
set -e
cd "$(dirname "$0")"

DLL="InstancedTimer/bin/Release/InstancedTimer.dll"
[ -f "$DLL" ] || { echo "missing $DLL - run ./build.sh first" >&2; exit 1; }
[ -f InstancedTimer/icon.png ] || { echo "missing InstancedTimer/icon.png - run tools/make_icon.py" >&2; exit 1; }

VERSION=$(grep -o '"version_number": *"[^"]*"' InstancedTimer/manifest.json | sed 's/.*"\([0-9.]*\)"/\1/')
STAGE=$(mktemp -d)
OUT="InstancedTimer-v${VERSION}.zip"

mkdir -p "$STAGE/Plugins"
cp "$DLL" "$STAGE/Plugins/InstancedTimer.dll"
cp InstancedTimer/manifest.json "$STAGE/manifest.json"
cp README.md "$STAGE/README.md"
cp InstancedTimer/icon.png "$STAGE/icon.png"
cp CHANGELOG.md "$STAGE/CHANGELOG.md" 2>/dev/null || true

rm -f "$OUT"
(cd "$STAGE" && zip -qr "$OLDPWD/$OUT" .)
rm -rf "$STAGE"
echo "packaged: $OUT"
unzip -l "$OUT"
