# Fase activa — Sprint 4, Fase 4

**Actualizado:** 2026-09-02  
**Modo actual:** sub-lote **4B fechado** → iniciar **4C** (docs `07`, `08`, `09`)

## Estado em uma linha

Sub-lote **4B (Nutrition)** implementado e gate aprovado com **1534** testes Release verdes.
Próximo: sub-lote **4C** após aprovação explícita do utilizador.

## Workflow desta fase (4 papéis)

| Papel | Ferramenta típica | O que fazer |
|---|---|---|
| Planear / rever plano | Claude Code, Codex | Ler este ficheiro + Sprint Pack; **não** explorar o repo inteiro |
| Gerar blueprints | Claude Code, Codex | Skill `graphify-pseudocode` ou `sprint-context` modo `blueprint` |
| Implementar | Tu (manual) | Seguir ordem dos docs `01`–`15`; Cursor só para dúvidas e ajustes pontuais |
| Testes + review | Claude Code, Codex, Cursor | Skill `sprint-context` modo `review`; correr `dotnet test` com evidência |

## Ler nesta ordem (obrigatório antes de planear ou rever)

1. Este ficheiro (`.claude/memory/ACTIVE.md`)
2. Sprint Pack: `.claude/project/sprints/sprint-4/fase-4/README.md`
3. Sessão: `.claude/memory/Sessions/2026-09-02-fase4-blueprints-completos.md`
4. Índice local dos blueprints: `docs/backend-files/sprint_4/fase_4/00_indice_ordem_e_decisoes.md`
5. Documento do sub-lote em curso (ver tabela abaixo)
6. Código **apenas** nos caminhos listados em «Superfície afectada»

## Sub-lotes e ordem

| Sub-lote | Docs | Gate | Casos de uso |
|---|---|---|---|
| 4A | `01`, `02`, `03` | Gate 4A | 18 |
| **4B** ← concluído | `04`, `05`, `06` | Gate 4B | 20 |
| **4C** ← actual | `07`, `08`, `09` | Gate 4C | 45 |
| 4D | `10`–`15` | Gate 4D | 32 + portal |

**Regra:** um sub-lote só começa depois do gate anterior aprovado.

## Blocker conhecido (corrigir no doc 01 / sub-lote 4A)

`JwtAccessTokenIssuer` emite claim `trainerId`; `ApiClaimNames` e `TenantContextMiddleware` exigem `trainer_id` com `MapInboundClaims = false`. Tokens reais falham para trainer e cliente.

## Decisões em aberto (utilizador)

1. Adaptadores de Infrastructure das 4 portas do Client Portal — recomendado doc `11b` dentro do 4D.
2. Binder de paginação partilhado — bloco duplicado em 11 controllers; QG4C-DRY-002.

## Superfície afectada (não fazer grep global fora disto)

```
backend/src/Api/Controllers/
backend/src/Api/Contracts/
backend/src/Application/Features/ClientPortal/
backend/tests/Api.FunctionalTests/
```

Controllers existentes (baseline): `AuthController`, `AdminContentModerationController`.

## Fora de âmbito desta fase

- Sprint 5: `ReplaceLogo`, `CreateCheckout`, `CreateCustomerPortal`, `ProcessPaymentWebhook`
- Migrations EF (Fase 4 não altera schema)
- Trainers administrativos read-only (rotas `Remove` na matriz Fase 0)

## Evidência de fecho esperada

- `dotnet test PTManager.sln --configuration Release` verde
- Gates em `docs/backend-files/sprint_4/fase_4/03`, `06`, `09`, `13`, `15` e `backlogs/QualityGates.md`
- Nota de sessão em `.claude/memory/Sessions/` + actualizar este ficheiro para a próxima fase

## Graphify

Regenerar **no fecho de cada Sprint** (não por fase intermédia). Última geração:
**2026-09-02** (9262 nós). Ver `.claude/project/sprints/GRAPHIFY.md`.
