#!/bin/bash
# A development build of the player into Builds/Dev (ignored by git). Takes a
# few minutes. Prints the build result line and exits non-zero on failure.
HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$HERE/.." && pwd)"
LOG="$HERE/.check/build.log"
mkdir -p "$HERE/.check"

"$HERE/unity.sh" -executeMethod PlayerBuild.Dev -logFile "$LOG" >/dev/null 2>&1
if grep -q "GAME BUILD: Succeeded" "$LOG"; then
  echo "built: $PROJECT/Builds/Dev/TileWorld.app"
else
  echo "BUILD FAILED"; grep -E "error CS|could not be found|Exception" "$LOG" | head -8
  exit 1
fi
