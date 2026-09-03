#!/bin/bash
# Runs a probe: a throwaway script that is dropped into the game, builds a
# player with it, runs the player until the probe writes "done" to its stage
# file, and gathers what it wrote and photographed.
#
#   Tools/run-probe.sh Tools/probe/StructureTour.cs.txt [seconds]
#
# The probe is copied to Assets/Scripts/_Probe.cs for the build and removed
# afterwards; it must never be committed. Screenshots the probe saves into
# Application.persistentDataPath are moved to Tools/.check/shots/. Back the
# saves up first (save-backup.sh) and restore them after (save-restore.sh):
# a probe that teleports the player changes the save.
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$HERE/.." && pwd)"
PROBE="$1"; WAIT="${2:-300}"
OUT="$HERE/.check"; SHOTS="$OUT/shots"
SAVE="$HOME/Library/Application Support/DefaultCompany/Tile World"
STAGES="$OUT/probe-stages.txt"

mkdir -p "$SHOTS"
sed "s#__STAGES__#$STAGES#g" "$PROBE" > "$PROJECT/Assets/Scripts/_Probe.cs"
"$HERE/check.sh" || { rm -f "$PROJECT/Assets/Scripts/_Probe.cs"*; exit 1; }
"$HERE/build.sh" || { rm -f "$PROJECT/Assets/Scripts/_Probe.cs"*; exit 1; }
rm -f "$PROJECT/Assets/Scripts/_Probe.cs" "$PROJECT/Assets/Scripts/_Probe.cs.meta"

# earlier shots are kept; a probe's own overwrite by name
rm -f "$STAGES" "$OUT/probe.log" "$SAVE"/probe-*.png
"$PROJECT/Builds/Dev/TileWorld.app/Contents/MacOS/Tile World" -logFile "$OUT/probe.log" \
    -screen-width 1400 -screen-height 900 -screen-fullscreen 0 >/dev/null 2>&1 &
PID=$!
for i in $(seq 1 $((WAIT / 2))); do sleep 2; grep -q " done" "$STAGES" 2>/dev/null && break; done
sleep 1; kill $PID 2>/dev/null

echo "--- stages ---"; cat "$STAGES" 2>/dev/null
echo "exceptions in log: $(grep -c Exception "$OUT/probe.log")"
python3 - "$SAVE" "$SHOTS" <<'PY'
import glob, os, sys
from PIL import Image
for p in sorted(glob.glob(os.path.join(sys.argv[1], "probe-*.png"))):
    Image.open(p).convert("RGB").crop((0, 40, 1400, 890)).save(os.path.join(sys.argv[2], os.path.basename(p)))
    os.remove(p); print("shot", os.path.basename(p))
PY
