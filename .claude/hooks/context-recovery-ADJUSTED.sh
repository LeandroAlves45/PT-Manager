#!/bin/bash
# Re-injects critical project rules after context compaction.
# Used as a SessionStart hook with matcher "compact".

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

# Git commands only if in a git repository
CONTEXT=""
if [ -d "$ROOT/.git" ]; then
  BRANCH=$(git -C "$ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null)
  if [ -n "$BRANCH" ] && [ "$BRANCH" != "HEAD" ]; then
    CONTEXT="Branch: $BRANCH"
  fi

  LAST_COMMIT=$(git -C "$ROOT" log --oneline -1 2>/dev/null)
  if [ -n "$LAST_COMMIT" ]; then
    CONTEXT="$CONTEXT | Last commit: $LAST_COMMIT"
  fi

  CHANGES=$(git -C "$ROOT" status --porcelain 2>/dev/null | wc -l | tr -d ' ')
  if [ "$CHANGES" -gt 0 ] 2>/dev/null; then
    CONTEXT="$CONTEXT | Uncommitted changes: $CHANGES files"
  fi
fi

cat <<'HEADER'
=== CONTEXT RECOVERED AFTER COMPACTION ===

REGRA PERMANENTE: nao agir por suposicoes; verificar factos e confirmar informacoes
antes de tomar decisoes (ver CLAUDE.md).

Indice de toda a documentacao do projeto: .claude/memory/NEST.md

Ordem de prioridade em caso de conflito de informacao (ver AGENTS.md, seccao
"Prioridade de decisao"):
  1. Pedido explicito do utilizador
  2. AGENTS.md (regras tecnicas transversais, re-injetado a seguir)
  3. Documentos canonicos em .claude/project/ (arquitetura, schema, roadmap, dev guide)
  4. Codigo atual do repositorio
  5. .claude/memory/MEMORY.md e notas de sessao (auxiliar, nunca substitui o resto)
HEADER

[ -n "$CONTEXT" ] && echo "" && echo "Current state: $CONTEXT"

if [ -f "$ROOT/AGENTS.md" ]; then
  echo ""
  echo "=== AGENTS.md (re-injected) ==="
  cat "$ROOT/AGENTS.md"
fi

if [ -f "$ROOT/.claude/CLAUDE.md" ]; then
  echo ""
  echo "=== CLAUDE.md (re-injected) ==="
  cat "$ROOT/.claude/CLAUDE.md"
fi

echo ""
echo "=== END CONTEXT RECOVERY ==="
exit 0