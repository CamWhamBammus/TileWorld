#!/bin/bash
# Copies the player's world saves aside before a probe run. Restore with
# save-restore.sh, which puts back only the fields the run changed.
SAVE="$HOME/Library/Application Support/DefaultCompany/Tile World"
DEST="${1:-$(dirname "$0")/.check/savebackup}"
rm -rf "$DEST"; mkdir -p "$DEST"
cp -R "$SAVE/worlds" "$DEST/"
echo "backed up to $DEST"
