#!/bin/bash
# Stop hook: lembrete para manter GOTCHAS_BACKEND.md/GOTCHAS_FRONTEND.md/lessons.md atualizados.
if [ -n "${CLAUDE_PROJECT_DIR:-}" ]; then
  PROJECT_DIR="$CLAUDE_PROJECT_DIR"
else
  PROJECT_DIR="$(pwd)"
fi

BACKEND_FILE="$PROJECT_DIR/.claude/tasks/GOTCHAS_BACKEND.md"
FRONTEND_FILE="$PROJECT_DIR/.claude/tasks/GOTCHAS_FRONTEND.md"
LESSONS_FILE="$PROJECT_DIR/.claude/tasks/lessons.md"

echo "Lembrete: se nesta sessao houve correcoes do utilizador ao teu trabalho, regista o padrao em $BACKEND_FILE ou $FRONTEND_FILE (conforme a camada) e a licao em $LESSONS_FILE (ver CLAUDE.md, seccao Self-improvement Loop)."
exit 0
