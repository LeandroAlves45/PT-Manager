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

cat <<'RULES'
=== CONTEXT RECOVERED AFTER COMPACTION ===

CRITICAL PROJECT RULES (PT Manager)
Backend: Python 3.12, FastAPI, SQLModel, PostgreSQL, JWT, Stripe, APScheduler
Frontend: React 19, Vite, Tailwind, Chakra UI + shadcn/ui
SaaS multi-tenant — roles: superuser, trainer, client

1. LAYERED ARCHITECTURE - MANDATORY
   Ordem: api/routes -> services -> repositories -> db/models
   Routes: HTTP, Depends(auth), delegacao para services
   Services: logica de negocio, sem detalhes HTTP
   Repositories: queries SQLModel, sempre filtrar por trainer_id
   NUNCA colocar logica de negocio em routes
   NUNCA queries directas em routes (usar repositories)

2. MULTI-TENANT - NON-NEGOTIABLE
   Todas as queries filtram por trainer_id do JWT
   Nunca confiar em trainer_id do request body
   Client role so acede aos proprios dados

3. DATABASE MIGRATIONS - NON-NEGOTIABLE
   Migrations SQL em backend/app/db/migrations/
   Aplicar via: python -m app.db.migrate_runner
   NUNCA editar ficheiro SQL ja aplicado
   Nova alteracao = novo ficheiro numerado (NNN_descricao.sql)

4. ERROR HANDLING
   HTTPException nas routes com codigos corretos
   Logging estruturado, Sentry em producao
   Nunca expor stack traces ao cliente

5. TESTING REQUIREMENTS
   pytest no backend (unit + integration)
   Vitest no frontend
   Correr ficheiro especifico apos alteracoes
   Correr antes de marcar tarefa concluida: pytest / npm run test

6. SECURITY
   Secrets em environment variables (API_KEY, SECRET_KEY, STRIPE_*)
   JWT + API Key middleware em routes protegidos
   Stripe webhook: verificar HMAC
   Nunca logar senhas, tokens ou API keys

7. GIT WORKFLOW
   Feature branches, conventional commits
   Testes tem de passar antes de commit

COMMANDS:
  uvicorn app.main:app --reload --port 8000          # Start backend (from backend/)
  python -m app.db.migrate_runner                    # Apply migrations
  pytest                                             # Run backend tests
  ruff check app/ && ruff format app/                # Lint/format Python
  npm run dev                                        # Start frontend (from frontend/)
  npm run test                                       # Run frontend tests
RULES

[ -n "$CONTEXT" ] && echo "" && echo "Current state: $CONTEXT"

if [ -f "$ROOT/.claude/CLAUDE.md" ]; then
  echo ""
  echo "=== CLAUDE.md (re-injected) ==="
  cat "$ROOT/.claude/CLAUDE.md"
fi

echo ""
echo "=== END CONTEXT RECOVERY ==="
exit 0