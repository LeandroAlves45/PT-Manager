# Sprint Pack — Sprint 6 (backend do layout + frontend)

Estado: **planeado (2026-09-16)**. Nenhuma fase iniciada; blueprints por gerar. Entrada
permitida: Gate 5D do código fechado (gates de provider R2/Stripe/Cloudinary podem ficar
abertos).

## Scope

Decisão backend-first de 2026-09-16: o layout aprovado (Claude Design) pressupõe
capacidades que o backend não tem. As fases 6A–6B fecham essas lacunas; o frontend
começa na 6C.

| Fase | Tipo | Resumo | Depende de |
|---|---|---|---|
| 6A | Backend — escrita e schema | RPE, porção padrão, check-in revisto, séries e concluir treino pelo cliente, tomas de suplementos; 1 migration | Gate 5D |
| 6B | Backend — leituras agregadas | Dashboard do trainer (com vendas de packs estimadas), resumo do cliente (adesão calculada), filtros, fila de moderação, treino de hoje | 6A |
| 6C | Frontend — Fundações | Vite + TS strict, tokens, AppShell com dropdown de perfil, cliente OpenAPI, sessão, seed, health, CI | 6B |
| 6D | Frontend — Admin | Fila de moderação, catálogos globais | 6C |
| 6E | Frontend — Trainer | Dashboard, clientes, sessões/packs, planos, biblioteca, check-ins, definições, billing, vídeo 5D | 6C |
| 6F | Frontend — Cliente | Treino de hoje com registo, nutrição, suplementos com tomas, check-ins, perfil, white-label | 6C |
| 6G | Frontend — Auth UX | Login final, signup, email, password, convite, Google | 6C |

Fora: categoria de alimento e notas de moderação (excluídos); CSV, notificações in-app e
pesquisa transversal (DEF-PROD-004 a 006).

## Ordem de leitura

1. `.claude/memory/ACTIVE.md`
2. Este README
3. `docs/backend-files/sprint_6/plan_sprint_6_por_atualizar.md` (ponto de entrada local)
4. `.claude/project/frontend/layout/01_RELATORIO_ANALISE.md`
5. Documento de layout do ecrã da fase (`layout/02`–`06`) e a imagem correspondente em
   `layout/assets/` (PNG; não abrir o Claude Design nem PDF)
6. `.claude/project/02_SPRINTS_ROADMAP.md` §Sprint 6
7. Código só nos paths listados no blueprint da fase

## Gates

Definidos em `02_SPRINTS_ROADMAP.md` §Sprint 6 (Gate 6A a Gate 6G). Os IDs `QG6*-*`
são atribuídos no blueprint de cada fase e registados em `backlogs/QualityGates.md`.

## Decisões abertas

Lista em `frontend/layout/01_RELATORIO_ANALISE.md` §8 (grupos musculares, "em atraso" das
tomas, escala de RPE, concluir treino parcial, janela da adesão, limiares de alertas,
semântica de `TargetDate`, top-N e fuso do dashboard). Fechar no blueprint da 6A/6B,
com verificação no código, antes de implementar.

## Blockers

- Screenshots em `.claude/project/frontend/layout/assets/` (6 PNG, 12 de 13 artboards);
  falta só o 08 Command palette (não bloqueia; há especificação textual).
- Assets finais do logo e favicons (Codex) antes da 6C.
