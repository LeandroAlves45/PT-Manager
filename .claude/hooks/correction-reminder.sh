#!/bin/bash
# Stop hook: lembrete para manter correction.md/lessons.md atualizados.
if [ -n "${CLAUDE_PROJECT_DIR:-}" ]; then
  PROJECT_DIR="$CLAUDE_PROJECT_DIR"
else
  PROJECT_DIR="$(pwd)"
fi

CORRECTION_FILE="$PROJECT_DIR/.claude/tasks/correction.md"
LESSONS_FILE="$PROJECT_DIR/.claude/tasks/lessons.md"

echo "Lembrete: se nesta sessao houve correcoes do utilizador ao teu trabalho, regista o padrao em $CORRECTION_FILE e a licao em $LESSONS_FILE (ver CLAUDE.md, seccao Self-improvement Loop)."
exit 0
