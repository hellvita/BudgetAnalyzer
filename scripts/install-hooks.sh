#!/bin/sh
# Copies the tracked pre-commit script into .git/hooks/ so git executes it.
# Run once after cloning: ./scripts/install-hooks.sh

ROOT="$(git rev-parse --show-toplevel)"
SRC="$ROOT/scripts/pre-commit"
DST="$ROOT/.git/hooks/pre-commit"

cp "$SRC" "$DST"
chmod +x "$DST"
echo "pre-commit hook installed."
