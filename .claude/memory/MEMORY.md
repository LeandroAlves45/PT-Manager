# PT Manager — Memória do Projeto

Índice de decisões, gotchas e estado. Atualizar após cada sessão relevante.

## Stack (confirmado)

- **Backend:** Python 3.12, FastAPI, SQLModel, PostgreSQL, JWT, Stripe
- **Frontend:** React 19, Vite, Tailwind, Chakra + shadcn (JSX pages, TSX ui/)
- **Deploy:** Render + Vercel

## Paths Críticos

| O quê            | Onde                              |
| ---------------- | --------------------------------- |
| App entry        | `backend/app/main.py`             |
| Models           | `backend/app/db/models/`          |
| Migrations SQL   | `backend/app/db/migrations/`      |
| Migration runner | `python -m app.db.migrate_runner` |
| Config/env       | `backend/app/core/config.py`      |
| Frontend router  | `frontend/src/App.jsx`            |
| API modules      | `frontend/src/api/`               |

## Gotchas Conhecidos

1. **Setup copiado de outro projeto** — root `.claude`/`.cursor` descreviam C#/.NET; em adaptação para FastAPI (Jul 2026)
2. **Migrations SQL** — não são EF Core/Alembic; ficheiros numerados, nunca editar existentes
3. **Multi-tenant** — sempre filtrar por `trainer_id`; setup antigo dizia "single user sem auth" — incorreto
4. **Frontend misto** — páginas `.jsx`, componentes shadcn `.tsx`; não forçar migração TS completa agora
5. **Repos incompletos** — 11 repos vs 21 routes; alguns services acedem DB directamente
6. **Páginas monolíticas** — `AssessmentPage.jsx` (~84KB), `MealsPlanPage.jsx` (~58KB)
7. **pyproject.toml** — description ainda diz "single-user" (stale)

## Decisões de Setup (Jul 2026)

- Manter todas as skills (sem remoção)
- Manter claude-mem nos hooks Cursor (porta 37777)
- Fonte de verdade: `.claude/` na raiz; `backend/.claude/` consolidado para pointer
- Documentação de referência criada: `architecture.md`, `database-schema.md`, `security-conventions.md`, `clean-architecture-guide.md`

## Próximos Passos (Refactoração — fora do setup)

- [x] Analisar dependências e preparar plano faseado (Jul 2026)
- [ ] Implementar `.claude/dependency-update-plan.md`
- [ ] Extrair componentes de páginas monolíticas
- [ ] Completar repository layer
- [ ] Refactoração do Backend todo e ajustar testes
- [ ] Adicionar React Query para cache/invalidação
- [ ] Aumentar cobertura de testes frontend

## Dependency Update Plan (Jul 2026)

- Analisadas 85 dependências diretas únicas: 26 backend e 59 frontend.
- Backend sem lockfile Python; `requirements.txt` tem pins exatos e
  `pyproject.toml` intervalos divergentes. SQLAlchemy é apenas transitiva.
- Frontend tem `package-lock.json` v3 com 651 pacotes resolvidos.
- Estado npm: 18 vulnerabilidades (10 altas, 6 moderadas, 2 baixas);
  prioridades imediatas são Axios 1.18.1, React Router DOM 7.18.1,
  PostCSS 8.5.17 e Vite 7.3.6.
- Manter React 19 e atualizar para 19.2.7; não há migração major React.
- Majors a isolar: Vite 8/plugin React 6, ESLint 10, TypeScript 7,
  Lucide 1, React Day Picker 10, Node types 26, Globals 17 e Speed Insights 2.
- Snyk não autenticou; executar `pip-audit` ou Snyk autenticado antes dos bumps.
- Plano completo: `.claude/dependency-update-plan.md`.
- Tabela e gotchas: `.claude/memory/gotcha_dependency_updates.md`.
- Checklist: `tasks/todo.md`.
