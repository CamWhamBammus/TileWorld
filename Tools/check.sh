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
