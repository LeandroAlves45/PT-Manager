---
paths:
  - "backend/src/Infrastructure/Data/**"
  - "backend/src/Infrastructure/Data/Migrations/**"
---

# Database Migrations — PT Manager

- **Nunca modificar uma migration EF Core já aplicada.** Gerar uma nova migration (`dotnet ef migrations add <Nome>`) para qualquer alteração de schema. Migrations existentes podem já ter corrido num ambiente partilhado.
- Migrations são sempre geradas via EF Core (`dotnet ef migrations add ... --project ... --startup-project ...`), nunca escritas à mão.
- PostgreSQL (Neon) é a fonte de verdade do schema.
- Nunca executar migrations automaticamente no arranque da API.
- Nunca converter ou reaproveitar as migrations Python antigas (`backend-python/app/db/migrations/**`) — não definem a arquitetura de destino.
- Testar migrations localmente ou via Testcontainers antes de commitar.
- Nunca eliminar colunas ou tabelas sem confirmar que os dados deixaram de ser necessários.
- Adicionar índices na mesma migration que introduz a coluna quando as queries vão filtrar por ela.
- Aplicar Global Query Filters para multi-tenancy (`trainer_id`) ao nível do `DbContext`.
