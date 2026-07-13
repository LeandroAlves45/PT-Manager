#!/bin/bash
# Prints a short project banner at the start of every Claude Code session.
# Used as a SessionStart hook (matcher: "").

find_project_root() {
  local dir="$PWD"
  while [ "$dir" != "/" ]; do
    if [ -d "$dir/.claude" ] || [ -d "$dir/.git" ]; then
      echo "$dir"
      return 0
    fi
    dir=$(dirname "$dir")
  done
  echo "$PWD"
  return 0
}

# Executa tudo num subshell para evitar contaminar o estado
(
  ROOT=$(find_project_root)
  
  echo "=== PT Manager ==="
  echo "Stack: Python 3.12 / FastAPI / SQLModel / PostgreSQL + React 19 / Vite / Tailwind"
  
  # Trata git apenas se existir .git
  if [ -d "$ROOT/.git" ]; then
    BRANCH=$(git -C "$ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null)
    if [ -n "$BRANCH" ] && [ "$BRANCH" != "HEAD" ]; then
      echo "Branch atual: $BRANCH"
    fi
    
    LAST_COMMIT=$(git -C "$ROOT" log --oneline -1 2>/dev/null)
    if [ -n "$LAST_COMMIT" ]; then
      echo "Ultimo commit: $LAST_COMMIT"
    fi
  fi
  
  # Memory - verifica antes de tentar ler
  MEMORY_FILE="$ROOT/.claude/memory/MEMORY.md"
  if [ -f "$MEMORY_FILE" ]; then
    echo ""
    echo "--- MEMORY.md ---"
    cat "$MEMORY_FILE"
  fi
  
  # Todo - verifica antes de tentar ler
  TODO_FILE="$ROOT/tasks/todo.md"
  if [ -f "$TODO_FILE" ]; then
    echo ""
    echo "--- tasks/todo.md ---"
    cat "$TODO_FILE"
  fi
  
  echo "========================================"
)

exit 0