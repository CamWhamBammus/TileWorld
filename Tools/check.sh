#!/bin/zsh
# Compiles every runtime script with Unity's own Roslyn, in a few seconds,
# without opening the editor. This is the first thing to run after any edit.
#
# What it cannot see: Editor scripts (Assets/Editor and any */Editor/*), and
# anything an Editor-only define would include. DEVELOPMENT_BUILD is defined
# here so the dev panel (DevTools.cs) is checked; a real build still proves
# the rest. The source list is gathered fresh on every run -- a fixed list
# silently missed every new file.
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$HERE/.." && pwd)"
UNITY_CONTENTS="${UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/6000.2.13f1/Unity.app/Contents}"
OUT="$HERE/.check"
mkdir -p "$OUT"

DOTNET="$UNITY_CONTENTS/NetCoreRuntime/dotnet"
CSC="$UNITY_CONTENTS/DotNetSdkRoslyn/csc.dll"
[ -x "$DOTNET" ] || { echo "CHECK UNAVAILABLE: no dotnet at $DOTNET"; exit 2; }

RSP="$OUT/compile.rsp"
{
  echo "-target:library"
  echo "-nostdlib+"
  echo "-nologo"
  echo "-define:DEVELOPMENT_BUILD"
  echo "-out:\"$OUT/check.dll\""
  sed "s#\${UNITY_CONTENTS}#$UNITY_CONTENTS#g; s#\${PROJECT}#$PROJECT#g" "$HERE/check.refs"
  find "$PROJECT/Assets/Scripts" "$PROJECT/Assets/StarterAssets" "$PROJECT/Assets/Low Poly Isometric Tiles - Cartoon Pack" \
       -name '*.cs' -not -path '*/Editor/*' | sed 's/^/"/;s/$/"/'
} > "$RSP"

RESULT=$("$DOTNET" "$CSC" "@$RSP" 2>&1)
if [ $? -ne 0 ]; then
  echo "$RESULT" | grep -E "error" | head -12
  echo "-- compile FAILED --"
  exit 1
fi
echo "compiles clean (checked)"

# And the things the compiler cannot see: a colour painted over a swatch the building kit reads,
# or a swatch list that has slid out of order. Both have happened and neither errors anywhere.
# Quiet when clean: this runs before every build and every probe, and three lines of "0 problems"
# on each of those is three lines of noise over the one line that will matter one day.
CHECKS=""
if command -v python3 >/dev/null 2>&1 && [ -f "$HERE/palette_add.py" ]; then
  CHECKS="$CHECKS$(python3 "$HERE/palette_add.py" check | grep -Ev "^(shared |[0-9]+ swatches)")"
fi

# And the tile ids: a definition with no library entry, two definitions with the same number, or a
# range an editor tool writes that has no assets behind it. The reef and the fill blocks collided
# over ids 95 and 96 once and the fills have had to move twice since.
if command -v python3 >/dev/null 2>&1 && [ -f "$HERE/ids.py" ]; then
  CHECKS="$CHECKS$(python3 "$HERE/ids.py" | grep -Ev "^([0-9]+ definitions|spare |[0-9]+ problems)")"
fi

# And whether a hash still has bits where the code reaches for them. Two places took a slice
# from too far up one and could only ever reach half its range: every plant in the world stood
# on one side of its tile, in rows.
if command -v python3 >/dev/null 2>&1 && [ -f "$HERE/bits.py" ]; then
  CHECKS="$CHECKS$(python3 "$HERE/bits.py" | grep -Ev "^[0-9]+ slices")"
fi

# And the numbers written down in both languages: where a tile's top is, how deep its body goes,
# how wide it is. The Blender scripts decide those and the game assumes them.
if command -v python3 >/dev/null 2>&1 && [ -f "$HERE/shapes.py" ]; then
  CHECKS="$CHECKS$(python3 "$HERE/shapes.py" | grep -Ev "^[0-9]+ shapes")"
fi

if [ -n "$CHECKS" ]; then
  echo "$CHECKS"
  echo "-- the sheet, the ids or the shapes are wrong; the compiler cannot see any of it --"
  exit 1
fi
