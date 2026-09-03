# Fase activa — Sprint 4, fase 5 (a iniciar)

**Actualizado:** 2026-09-03
**Modo actual:** **Sprint 4 Fase 4 FECHADA** → planear fase 5

## Estado em uma linha

Sprint 4 Fase 4 **concluída**: 115 endpoints de negócio, **1780** testes Release verdes,
gates 4A–4D e contrato (doc 14/15) verificados. Documento de fecho:
`docs/backend-files/sprint_4/fase_4/16_fase_4_finalizada.md`.

## Último marco (Fase 4)

| Sub-lote | Estado | Testes novos (aprox.) |
|---|---|---|
| 4A | ✅ | baseline |
| 4B | ✅ | ~40 |
| 4C | ✅ | 138 |
| 4D | ✅ | 108 + 41 contrato |

Bug corrigido no fecho 4D: `ClientSupplementAssignmentQueries` (500 em GET/List trainer).

## Ler nesta ordem (próximo sprint) ainda faltando a fase 5 do sprint 4

1. Este ficheiro
2. Fase 5 do sprint 4
2. `.claude/project/02_SPRINTS_ROADMAP.md` — Sprint 5
3. Sprint Pack Sprint 5 quando existir em `.claude/project/sprints/`
4. `.claude/memory/Sessions/2026-09-03-fase4-fecho-completo.md`

## Fora de âmbito imediato (Sprint 5)

- `ReplaceLogo`, `CreateCheckout`, `CreateCustomerPortal`, `ProcessPaymentWebhook`
- Avatar moderado / Cloudinary
- Regenerar Graphify (fecho Sprint 4 — ver `GRAPHIFY.md`)

## Blockers

Nenhum.

## Evidência de fecho Fase 4

- `dotnet test PTManager.sln --configuration Release` → 1780 passed, 1 skipped
- `backlogs/QualityGates.md` — secção Sprint 4 Fase 4 toda `[x]`
- Snapshot: `docs/api/api-surface.v1.txt`
