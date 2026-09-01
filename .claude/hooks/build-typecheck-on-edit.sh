#!/bin/bash
# Fast, scoped build/lint check after Edit|Write on production code.
# .cs files: dotnet build only the nearest .csproj.
# .ts/.tsx files: eslint on the single file.

if ! command -v jq >/dev/null 2>&1; then
  exit 0
fi

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
[ -z "$FILE_PATH" ] && exit 0
[ ! -f "$FILE_PATH" ] && exit 0

case "$FILE_PATH" in
  *.cs)
    DIR=$(dirname "$FILE_PATH")
    while [ "$DIR" != "/" ] && [ -z "$(find "$DIR" -maxdepth 1 -iname '*.csproj' 2>/dev/null)" ]; do
      DIR=$(dirname "$DIR")
    done
    CSPROJ=$(find "$DIR" -maxdepth 1 -iname '*.csproj' 2>/dev/null | head -1)
    if [ -n "$CSPROJ" ] && command -v dotnet >/dev/null 2>&1; then
      if ! dotnet build "$CSPROJ" --nologo -v q >/tmp/pt-build-check.log 2>&1; then
        echo "Build falhou em $CSPROJ apos editar $FILE_PATH:" >&2
        tail -n 30 /tmp/pt-build-check.log >&2
        exit 2
      fi
    fi
    ;;
  *.ts|*.tsx)
    if [ -n "${CLAUDE_PROJECT_DIR:-}" ] && [ -f "$CLAUDE_PROJECT_DIR/frontend/node_modules/.bin/eslint" ]; then
      OUT=$("$CLAUDE_PROJECT_DIR/frontend/node_modules/.bin/eslint" "$FILE_PATH" 2>&1)
      if [ $? -ne 0 ]; then
        echo "$OUT" | tail -n 30 >&2
        exit 2
      fi
    fi
    ;;
esac

exit 0
