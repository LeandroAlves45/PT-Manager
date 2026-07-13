#!/bin/bash
# Formats the file that was just written or edited.
# Used as a PostToolUse hook for Edit|Write operations.
# Python files: ruff format. TS/JS/JSON/CSS files: prettier (frontend).

if ! command -v jq >/dev/null 2>&1; then
  exit 0
fi

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
[ -z "$FILE_PATH" ] && exit 0
[ ! -f "$FILE_PATH" ] && exit 0

case "$FILE_PATH" in
  *.py)
    command -v ruff >/dev/null 2>&1 && ruff format "$FILE_PATH" >/dev/null 2>&1
    ;;
  *.ts|*.tsx|*.js|*.jsx|*.json|*.css)
    if [ -f "$CLAUDE_PROJECT_DIR/frontend/node_modules/.bin/prettier" ]; then
      "$CLAUDE_PROJECT_DIR/frontend/node_modules/.bin/prettier" --write "$FILE_PATH" >/dev/null 2>&1
    elif command -v npx >/dev/null 2>&1; then
      npx --no-install prettier --write "$FILE_PATH" >/dev/null 2>&1
    fi
    ;;
esac

exit 0
