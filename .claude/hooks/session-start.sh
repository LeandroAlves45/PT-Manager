#!/bin/bash
# Prints a short project banner at the start of every Claude Code session.
# Used as a SessionStart hook (matcher: "").

find_project_root() {
  local dir="$PWD"
  while [ "$dir" != "/" ]; do
    if [ -d "$dir/.claude" ] || [ -d "$dir/.git" ]; then
      echo "$dir"
      return
    fi
    dir=$(dirname "$dir")
  done
  echo "$PWD"
}

ROOT=$(find_project_root)
BRANCH=$(git -C "$ROOT" branch --show-current 2>/dev/null)
LAST_COMMIT=$(git -C "$ROOT" log --oneline -1 2>/dev/null)

echo "=== PT Manager ==="
echo "Stack: Python 3.12 / FastAPI / SQLModel / PostgreSQL + React 19 / Vite / Tailwind"
[ -n "$BRANCH" ] && echo "Branch atual: $BRANCH"
[ -n "$LAST_COMMIT" ] && echo "Ultimo commit: $LAST_COMMIT"
if [ -f "$ROOT/.claude/memory/MEMORY.md" ]; then
  echo ""
  echo "--- MEMORY.md ---"
  cat "$ROOT/.claude/memory/MEMORY.md"
fi
if [ -f "$ROOT/tasks/todo.md" ]; then
  echo ""
  echo "--- tasks/todo.md ---"
  cat "$ROOT/tasks/todo.md"
fi
echo "========================================"
exit 0
