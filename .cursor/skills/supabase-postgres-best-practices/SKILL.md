---
name: postgres-sqlmodel-best-practices
description: Boas práticas de performance PostgreSQL para PT Manager via SQLModel/SQLAlchemy. Usar ao escrever queries, desenhar schema, ou investigar lentidão.
metadata:
  version: "1.0.0"
  project: PT Manager
  abstract: Guia de otimização PostgreSQL para FastAPI + SQLModel multi-tenant. Cobre índices, N+1, migrations SQL e connection pooling.
---

# PostgreSQL + SQLModel Best Practices — PT Manager

Guia de performance PostgreSQL para o stack real do PT Manager (FastAPI + SQLModel + PostgreSQL).

## Quando Aplicar

- Escrever ou rever queries SQLModel/SQLAlchemy
- Desenhar entidades, relações ou migrations SQL em `backend/app/db/migrations/`
- Investigar lentidão em listagens de clientes, sessões, ou planos
- Configurar connection pooling (psycopg2)

## Áreas Prioritárias

| Prioridade | Área | Foco no PT Manager |
|---|---|---|
| 1 | N+1 queries | `selectinload`/`joinedload` ao carregar relações |
| 2 | Índices | `trainer_id` em tabelas tenant-scoped, FKs frequentes |
| 3 | Multi-tenant | Todas as queries filtram por `trainer_id` |
| 4 | Migrations SQL | Novo ficheiro numerado, nunca editar existente |
| 5 | Paginação | Listagens grandes devem paginar |

## Regras Práticas

### Evitar N+1

```python
from sqlalchemy.orm import selectinload

statement = (
    select(Client)
    .where(Client.trainer_id == trainer_id)
    .options(selectinload(Client.sessions))
)
```

### Índices

- Nova query frequente que filtra por coluna → índice na mesma migration
- `trainer_id` deve ter índice em tabelas tenant-scoped

### Migrations

- Um ficheiro SQL por alteração: `NNN_descricao.sql`
- Aplicar via `python -m app.db.migrate_runner`
- Nunca editar migration já aplicada

### Connection Pooling

- psycopg2 com pool por omissão via SQLAlchemy engine
- Render: connection string com pooling adequado para web service

## Fora de Âmbito

- Supabase RLS e client SDK — este projeto usa SQLModel directamente
- EF Core / Alembic — migrations são SQL manual

## Referências

Ver também ficheiros em `references/` desta skill para dicas gerais de PostgreSQL (indexação, vacuum, etc.) — ignorar secções específicas de Supabase RLS.
