# Session: Planeamento do Sprint 1 (Domain Layer) — 2026-07-25

**Focus:** Sprint 1 Domain Layer planeado e posteriormente implementado
**Status:** Entidades, Value Objects e portas implementados; testes unitários em curso

## What I Did

- Criados 17 ficheiros em `docs/backend-files/sprint_1/` (00_indice a
  16_avaliacao_sprint1): pseudocódigo alargado com XML docs completos para as
  27 entidades de domínio, 5 Value Objects, portas (`ITenantContext`, `IClock`),
  `DomainException`, configuração de XML docs para Swagger e plano de ~30 testes
  unitários.
- Atualizado `.claude/PT_Manager_Schema_v3.html` para refletir o schema real de
  `01_DATABASE_SCHEMA.md` (o HTML mostrava tabelas antigas: `workouts`,
  `billing_cycles`, `invoices`, `body_metrics`).
- Implementadas as 27 entidades de Domain, 5 Value Objects, `DomainException`,
  `ITenantContext` e `IClock`.
- Validado em 27/07/2026:
  `dotnet build PTManager.sln --configuration Release --no-restore` com
  0 warnings e 0 erros.
- `dotnet format PTManager.sln --verify-no-changes --no-restore` passou.
- `Domain.UnitTests` tem 10 testes implementados em 2 ficheiros, todos passing;
  os outros 12 ficheiros continuam placeholders para o plano do doc 15.

## Key Decisions Made

- **`InviteToken` incluído em `Identity/`** apesar de não constar da lista do
  roadmap — a tabela `invite_tokens` existe no schema e é necessária para a
  migration `InitialCreate` (28 tabelas). Aprovado pelo utilizador.
- **Repositórios/query services adiados** para o Sprint 2/3 (YAGNI): interfaces
  sem consumidor não se desenham bem. Desvio consciente face ao roadmap,
  registado em `13_portas_dominio.md`.
- **`GenerateDocumentationFile=true` global** no `Directory.Build.props`.
  CS1591 fica silenciado globalmente: documentam-se métodos com comportamento e
  contratos relevantes sem exigir XML em todas as propriedades públicas.
- **Agregado apenas em `MealPlan`** (invariantes `unique_meal_order` /
  `unique_supplement_per_meal`); Training fica plano — estrutura segue os
  invariantes, não a simetria.
- **Smart enums (records)** para `SubscriptionStatus`/`Tier`/`JobStatus`;
  strings simples para `Role`, `MealType`, etc. Critério documentado em
  `12_value_objects.md`.

## Learnings / Gotchas Detetados

- `training_plans` não tem CHECK `date_order` (assimetria com `meal_plans`) —
  Domain aplica o invariante; Sprint 2 deve avaliar acrescentar o CHECK.
- Coluna `fats_target` sem sufixo `_g` em `meal_plans` — atenção na Fluent API.
- `day_of_week` do schema é Monday=0; `System.DayOfWeek` é Sunday=0 — usar `int`.
- `supplements.created_by_user_id` é `ON DELETE SET NULL` (autoria), não posse
  de tenant — filtro multi-tenant diferente de `foods`/`exercises`.
- `ClientSessionPack.ExpirationDate` é o nome do Domain e será mapeado
  explicitamente para a coluna `expiry_date`.
- `JobStatus.DeadLetter` tem de persistir exatamente `dead_letter`; `deadletter`
  viola o CHECK do PostgreSQL.
- Instâncias `static readonly` de smart enums não podem ser usadas como constant
  patterns num switch C#; usar igualdade explícita.
- Value Objects não nullable materializados pelo EF através de construtor privado
  usam `= null!` para satisfazer a análise de nullability sem alterar o modelo.
- Guards de limites de `VARCHAR` têm de existir tanto no construtor como nos
  métodos de update; validação assimétrica permite estados que falham no INSERT
  ou UPDATE.
- Valores obrigatórios devem ser validados antes de `Trim`; caso contrário,
  input `null` produz `NullReferenceException` em vez de `DomainException`.
- O `.gitattributes` fixa LF para ficheiros .NET e o format completo está passing.
- `EmailAddress.Value` preserva o casing após `Trim`; apenas `Normalized` usa
  uppercase invariante. Email vazio tem erro próprio.
- O exit code 0 de `dotnet test` não basta: confirmar sempre testes descobertos,
  executados e respetivo total.

## Next Steps

1. Implementar os testes restantes do doc 15.
2. Antes do primeiro commit funcional: limpar `Api.http` e ignorar
   `backend/artifacts/`.
3. Antes da `InitialCreate`: fechar integridade cross-tenant, owner/renovação de
   leases e recuperação da outbox. O default de subscription status já foi
   corrigido para `ACTIVE`.
