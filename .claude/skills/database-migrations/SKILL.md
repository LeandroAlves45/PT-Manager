---
name: database-migrations
description: Expert em migrations EF Core para PostgreSQL no PT Manager. Garante migrations geradas (nunca escritas à mão), nunca editar uma migration já aplicada, e schema consistente entre dev e produção.
color: purple
emoji: 📊
---

# EF Core Migrations Specialist — PT Manager

Especialista em gestão de schema via migrations do Entity Framework Core, para PostgreSQL (Npgsql provider). Ver `.claude/project/01_DATABASE_SCHEMA.md` para a spec do modelo alvo.

## Core Mission

### Criar Migrations
- Migration nunca escrita à mão — sempre gerada a partir do modelo EF Core:
  ```bash
  dotnet ef migrations add DescricaoDaAlteracao --project src/Infrastructure
  ```
- Nome descritivo do que muda (`AddClientNotes`, não `Update1`)
- Rever sempre o diff gerado (`Migrations/*.cs` + snapshot) antes de aplicar

### Aplicar Migrations
- Desenvolvimento: `dotnet ef database update --project src/Infrastructure`
- Produção (Render free tier, sem pre-deploy command): passo de release controlado e manual, nunca `Database.Migrate()` automático no arranque da API (`00_ARCHITECTURE.md §7.3`)

### Nunca Editar uma Migration Aplicada
- Uma migration já aplicada (em qualquer ambiente partilhado) é imutável
- `__EFMigrationsHistory` (gerida automaticamente pelo EF Core) regista as migrations aplicadas
- Uma correção é sempre uma migration nova (`dotnet ef migrations add FixX`)

## Critical Rules

### Schema Consistente Dev/Produção
- PostgreSQL 17 em ambos os ambientes (local ou Neon branch de teste)
- Mesmas migrations correm em ambos, testadas num branch Neon antes de produção

### Índices Acompanham o Padrão de Acesso
- Nova query frequente que filtra por coluna → `HasIndex` na configuração EF Core (`IEntityTypeConfiguration<T>`), incluído na mesma migration

### Multi-Tenant
- Novas entidades tenant-scoped devem incluir `OwnerTrainerId` com índice, e um Global Query Filter ligado a `ITenantContext` (nunca a `HttpContext` direto — `00_ARCHITECTURE.md §6.2`)
- Foreign keys para `users`/`clients` conforme o domínio

### IDs
- `Guid`/`uuid` nativo, gerado no construtor da entidade (`Guid.NewGuid()`), nunca `gen_random_uuid()` do Postgres nem string — ver `01_DATABASE_SCHEMA.md` Decisão 1

## PT Manager — Paths

| Recurso | Path |
|---------|------|
| DbContext | `src/Infrastructure/Data/PtManagerDbContext.cs` |
| Configurações de entidade | `src/Infrastructure/Data/Configurations/` (`IEntityTypeConfiguration<T>` por feature) |
| Migrations geradas | `src/Infrastructure/Migrations/` |
| Spec do schema alvo | `.claude/project/01_DATABASE_SCHEMA.md` |

## Nota sobre o Backend Python

`backend-python/` (fora do Git) tinha migrations SQL manuais via `migrate_runner.py` — esse histórico **não é convertido**. O schema C# parte de zero a partir de `01_DATABASE_SCHEMA.md` (`00_ARCHITECTURE.md §7.3`: "não existem dados de produção a preservar").
