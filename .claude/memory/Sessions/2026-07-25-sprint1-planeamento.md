# Session: Planeamento do Sprint 1 (Domain Layer) — 2026-07-25

**Focus:** Sprint 1 planeado em pseudocódigo alargado — nenhum código real escrito
**Status:** Documentação concluída; implementação aguarda pedido explícito

## What I Did

- Criados 17 ficheiros em `docs/backend-files/sprint_1/` (00_indice a
  16_avaliacao_sprint1): pseudocódigo alargado com XML docs completos para as
  27 entidades de domínio, 5 Value Objects, portas (`ITenantContext`, `IClock`),
  `DomainException`, configuração de XML docs para Swagger e plano de ~30 testes
  unitários.
- Atualizado `.claude/PT_Manager_Schema_v3.html` para refletir o schema real de
  `01_DATABASE_SCHEMA.md` (o HTML mostrava tabelas antigas: `workouts`,
  `billing_cycles`, `invoices`, `body_metrics`).

## Key Decisions Made

- **`InviteToken` incluído em `Identity/`** apesar de não constar da lista do
  roadmap — a tabela `invite_tokens` existe no schema e é necessária para a
  migration `InitialCreate` (28 tabelas). Aprovado pelo utilizador.
- **Repositórios/query services adiados** para o Sprint 2/3 (YAGNI): interfaces
  sem consumidor não se desenham bem. Desvio consciente face ao roadmap,
  registado em `13_portas_dominio.md`.
- **`GenerateDocumentationFile=true` global** no `Directory.Build.props` já no
  Sprint 1 (Swagger exige XML docs no Sprint 4); CS1591 silenciado só nos
  projetos de teste.
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

## Next Steps

1. Quando o utilizador pedir: implementar o código real do Sprint 1 seguindo os
   docs 01–15 (entidades → VOs → portas → testes → `dotnet test` verde).
2. Antes do primeiro commit funcional: normalizar CRLF de `Program.cs`, limpar
   `Api.http`, ignorar `backend/artifacts/` (pendências do Sprint 0).
