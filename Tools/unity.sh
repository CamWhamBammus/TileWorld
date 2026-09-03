#!/bin/bash
# Runs the Unity editor in batchmode for one -executeMethod, then puts back
# the one file every batchmode run wipes: Library/LastSceneManagerSetup.txt,
# which is what the editor reads to know which scene to open. Left empty, the
# editor comes up on a blank scene and the game looks broken when it is not.
#
#   Tools/unity.sh -executeMethod SomeEditorClass.Method -logFile /tmp/x.log
HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$HERE/.." && pwd)"
UNITY="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.2.13f1/Unity.app/Contents/MacOS/Unity}"

"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" "$@"
STATUS=$?

cat > "$PROJECT/Library/LastSceneManagerSetup.txt" <<'TXT'
sceneSetups:
- path: Assets/Scenes/SampleScene.unity
  isLoaded: 1
  isActive: 1
  isSubScene: 0
TXT

exit $STATUS
