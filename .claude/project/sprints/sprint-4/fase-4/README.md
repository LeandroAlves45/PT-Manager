# Sprint 4 — Fase 4: API de negócio + Client Portal

**Estado:** documentada (16 blueprints), **não implementada** em `backend/`  
**Sessão:** `.claude/memory/Sessions/2026-09-02-fase4-blueprints-completos.md`  
**Blueprints locais:** `docs/backend-files/sprint_4/fase_4/`  
**Quality gates:** `backlogs/QualityGates.md` (75 gates concretos)

## Objectivo

Expor 115 casos de uso de negócio já existentes na Application como HTTP, e criar o
Client Portal com casos de uso client-scoped novos.

## Métricas verificadas

| Métrica | Valor |
|---|---:|
| Casos de uso sem controller (baseline) | 115 |
| Controllers existentes antes da fase | 2 |
| Endpoints de negócio planeados | 115 |
| Exclusões Sprint 5 | 4 |
| Sub-lotes | 4 |
| Migrations nesta fase | 0 |

## Ordem de implementação

| # | Documento | Sub-lote | Resultado |
|---:|---|---|---|
| 1 | `01_fundacoes_api_partilhadas.md` | 4A | Base controller, paginação, JWT de teste, fix claim |
| 2 | `02_clients_trainersettings_billing.md` | 4A | Controllers 4A |
| 3 | `03_gate_4A.md` | 4A | Gate |
| 4 | `04_nutrition_contratos_e_controllers.md` | 4B | Nutrition |
| 5 | `05_nutrition_testes_e_query_budget.md` | 4B | Testes + query budget |
| 6 | `06_gate_4B.md` | 4B | Gate |
| 7 | `07_training_contratos_e_controllers.md` | 4C | Training |
| 8 | `08_sessions_packs_contratos_e_controllers.md` | 4C | Sessions + Packs |
| 9 | `09_gate_4C.md` | 4C | Gate |
| 10 | `10_assessments_supplements.md` | 4D | Assessments + Supplements |
| 11 | `11_client_portal_application.md` | 4D | Application portal |
| 12 | `12_client_portal_api.md` | 4D | ClientPortalController |
| 13 | `13_gate_4D.md` | 4D | Gate |
| 14 | `14_openapi_snapshot_e_matriz_contrato.md` | fecho | OpenAPI snapshot |
| 15 | `15_validacao_e_quality_gates_fase_4.md` | fecho | Auto-avaliação |

## Decisões fechadas (resumo)

Ver `00_indice_ordem_e_decisoes.md` §4. Pontos críticos:

- Client Portal = **novos** handlers client-scoped; nunca alterar handlers de trainer.
- Testes funcionais com JWT real + Testcontainers; não instanciar controllers à mão.
- Rate limiting geral já existe — só testar.
- Zero aliases; rotas `Remove` da matriz Fase 0 não se implementam.
- Sem migrations nesta fase.

## Blockers e riscos

| ID | Descrição | Onde corrigir |
|---|---|---|
| BLK-4A-001 | Claim `trainerId` vs `trainer_id` | Doc `01` |
| RISK-4B-001 | Override de `DbContext` em testes sem `TenantWriteValidationInterceptor` | Doc `05` |
| OPEN-001 | Adaptadores Infrastructure portal (4 portas) | Decisão utilizador / doc `11b` |
| OPEN-002 | Binder paginação partilhado | QG4C-DRY-002 |

## Referências canónicas (só secções relevantes)

- Auth e claims: `.claude/project/00_ARCHITECTURE.md` (JWT, multi-tenancy)
- Contrato HTTP: `AGENTS.md` (snake_case, `/api/v1`, Problem Details)
- Padrão blueprints: `.claude/memory/Patterns/blueprints_codigo_real_por_ficheiro.md`

## Modos de trabalho para IAs

| Modo | Ler | Evitar |
|---|---|---|
| **plan** | Este README + `00_indice` + sessão | Grep em todo `backend/` |
| **blueprint** | Doc alvo + Patterns + código dos paths em `surface.yaml` | Inventar tipos |
| **implement** (utilizador) | Doc N + ficheiros listados no doc | — |
| **review** | Gate do sub-lote + `dotnet test` + diff | Declarar verde sem correr testes |
