---
name: supabase-postgres-best-practices
description: Boas práticas de performance PostgreSQL para PT Manager via EF Core/Npgsql. Usar ao escrever queries, desenhar schema, ou investigar lentidão.
metadata:
  version: "2.0.0"
  project: PT Manager
  abstract: Guia de otimização PostgreSQL para ASP.NET Core + EF Core multi-tenant. Cobre índices, N+1, migrations EF Core e connection pooling.
---

# PostgreSQL + EF Core Best Practices — PT Manager

Guia de performance PostgreSQL para o stack C# do PT Manager (ASP.NET Core + EF Core 10 + Npgsql + PostgreSQL 17/Neon).

## Quando Aplicar

- Escrever ou rever queries EF Core (LINQ traduzido para SQL)
- Desenhar entidades, relações ou migrations em `src/Infrastructure/Data/`
- Investigar lentidão em listagens de clientes, sessões, ou planos
- Configurar connection pooling (Npgsql)

## Áreas Prioritárias

| Prioridade | Área | Foco no PT Manager |
|---|---|---|
| 1 | N+1 queries | `Include`/`ThenInclude` ao carregar relações, `AsSplitQuery` quando necessário |
| 2 | Índices | `owner_trainer_id` em tabelas tenant-scoped, FKs frequentes |
| 3 | Multi-tenant | Global Query Filters ligados a `ITenantContext`, nunca a `HttpContext` direto |
| 4 | Migrations EF Core | Sempre geradas (`dotnet ef migrations add`), nunca escritas à mão, nunca editar existente |
| 5 | Paginação | Listagens grandes devem paginar |

## Regras Práticas

### Evitar N+1

```csharp
var clients = await _context.Clients
    .Where(c => c.OwnerTrainerId == trainerId)
    .Include(c => c.Sessions)
    .AsSplitQuery() // evita cartesian explosion com múltiplos Includes
    .AsNoTracking() // leitura sem alteração — não precisa de change tracking
    .ToListAsync(ct);
```

### Índices

- Nova query frequente que filtra por coluna → índice na mesma migration EF Core (`HasIndex` na `IEntityTypeConfiguration<T>`)
- `owner_trainer_id` deve ter índice em tabelas tenant-scoped

### Migrations

- Sempre geradas: `dotnet ef migrations add DescricaoDaAlteracao --project src/Infrastructure`
- Aplicar via `dotnet ef database update --project src/Infrastructure`
- Nunca editar migration já aplicada — ver skill `database-migrations`

### Connection Pooling

- Npgsql com pooling nativo (`Pooling=true` na connection string, ativo por omissão)
- Neon fornece read replicas automáticas; connection pooling adequado para web service no Render

## Fora de Âmbito

- Supabase RLS e client SDK — este projeto usa EF Core diretamente contra Neon PostgreSQL, sem camada Supabase
- Migrations SQL manuais — o projeto usa exclusivamente migrations EF Core geradas

## Referências

Ver também ficheiros em `references/` desta skill para dicas gerais de PostgreSQL (indexação, vacuum, etc.) — ignorar secções específicas de Supabase RLS/client SDK.
