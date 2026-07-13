---
alwaysApply: true
description: "PT Manager project rules — stack, commands, patterns, protected files"
---

# PT Manager

SaaS multi-tenant para personal trainers.

Backend: Python 3.12, FastAPI, SQLModel, PostgreSQL 16, JWT, Stripe.
Frontend: React 19, Vite 7, Tailwind CSS 4, Chakra UI + shadcn/ui.
Deploy: Render (backend) + Vercel (frontend).

## Golden Rules

- Architecture: `backend/app/api/routes → services → repositories → db/models`
- Multi-tenant: todas as queries filtram por `trainer_id`
- Migrations: `python -m app.db.migrate_runner` — nunca editar SQL existente
- Testing: `pytest` (backend), Vitest (frontend)
- Docs: `.claude/architecture.md`, `database-schema.md`, `security-conventions.md`

## Commands

```bash
# backend/
uvicorn app.main:app --reload --port 8000
python -m app.db.migrate_runner
pytest
ruff check app/ && ruff format app/

# frontend/
npm run dev && npm run test && npm run lint
```

## Protected Files

- `.env`, `.env.*`, `secrets/`, `*.pem`, `*.key`
- `backend/app/db/migrations/**` — create new numbered SQL only

## Patterns

1. Backend feature: Model → Repository → Service → Route + Schema → Test
2. Multi-tenant: `trainer_id` from JWT, never from request body
3. Frontend: `api/` module → page; layouts by role
