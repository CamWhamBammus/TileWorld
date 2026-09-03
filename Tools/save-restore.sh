#!/bin/bash
# Puts back, field by field, whatever a probe run changed in the world saves.
# Only the fields that differ from the backup are touched, so anything the
# player did in between is kept. Never copy a whole save file back over a
# newer one: that has wiped real progress before.
SAVE="$HOME/Library/Application Support/DefaultCompany/Tile World"
FROM="${1:-$(dirname "$0")/.check/savebackup}"
for f in "$FROM"/worlds/*.json; do
  b=$(basename "$f")
  [ -f "$SAVE/worlds/$b" ] || continue
  python3 - "$f" "$SAVE/worlds/$b" <<'PY'
import json, sys
a = json.load(open(sys.argv[1])); c = json.load(open(sys.argv[2]))
changed = [k for k in sorted(set(a) | set(c)) if a.get(k) != c.get(k)]
if changed:
    for k in changed: c[k] = a[k]
    json.dump(c, open(sys.argv[2], "w"), indent=2)
    print(sys.argv[2].split("/")[-1], "restored:", ", ".join(changed))
PY
done
