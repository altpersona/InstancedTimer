#!/bin/sh
# Build InstancedTimer (client-only Valheim BepInEx mod).
# Paths default to this machine's install locations; override with:
#   ./build.sh -p:GameDir=/path/to/Valheim -p:ProfileDir=/path/to/profile
set -e
cd "$(dirname "$0")/InstancedTimer"
DOTNET="${DOTNET:-/home/default/.dotnet/dotnet}"
exec "$DOTNET" build -c Release "$@"
