---
alwaysApply: true
description: "PT Manager project rules — stack, commands, patterns, protected files"
---

# PT Manager

SaaS multi-tenant para personal trainers.

Backend: C# 14, .NET 10, ASP.NET Core, EF Core 10, PostgreSQL 17 (Neon), ASP.NET Core Identity.
Frontend: React 19, Vite 7, Tailwind CSS 4, Chakra UI + shadcn/ui.
Deploy: Render (backend) + Vercel (frontend) + Neon (PostgreSQL) + Upstash (Redis + QStash).

## Golden Rules

- Arquitetura: modular monolith / Clean Architecture — `Domain → Application → Infrastructure → Api`
- `Domain` não depende de frameworks nem de outros projetos da solução
- `Application` depende apenas de `Domain`; `Infrastructure` implementa as portas da `Application`
- Sem `IRepository<T>` genérico, `UnitOfWork` genérico, MediatR ou AutoMapper
- Multi-tenant: todas as queries filtram por `trainer_id` a partir do JWT, nunca do body/query/route
- Migrations: EF Core, geradas — nunca editar uma migration já aplicada
- Testing: xUnit + Testcontainers (backend), Vitest (frontend)
- Docs: `.claude/project/00_ARCHITECTURE.md`, `01_DATABASE_SCHEMA.md`, `02_SPRINTS_ROADMAP.md`, `03_DEVELOPER_GUIDE.md`

## Commands

```bash
# backend/
dotnet restore PTManager.sln
dotnet build PTManager.sln --configuration Release --no-restore
dotnet test PTManager.sln --configuration Release --no-build
dotnet format PTManager.sln --verify-no-changes --no-restore

# frontend/
npm run dev && npm run test -- --run && npm run lint
```

## Protected Files

- `.env`, `.env.*`, `secrets/`, `*.pem`, `*.key`, `*.p12`, `*.pfx`
- `backend/src/Infrastructure/Data/Migrations/**` — criar sempre uma migration EF Core nova
- `backend-python/` — apenas referência funcional local, não é alvo do projeto atual; não converter nem reescrever

## Patterns

1. Backend feature: `Domain` (entidade/invariantes) → `Application` (caso de uso, `Result`/`Result<T>`) → `Infrastructure` (implementação da porta) → `Api` (controller fino + Problem Details)
2. Multi-tenant: `trainer_id` extraído do JWT via `ITenantContext`, nunca do request body
3. Frontend: `api/` module → page; layouts por role (`superuser`, `trainer`, `client`)
