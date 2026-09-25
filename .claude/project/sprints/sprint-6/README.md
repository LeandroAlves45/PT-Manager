# Sprint Pack — Sprint 6 (backend do layout + frontend)

Estado em 2026-09-23: 6A e 6B fechadas no backend real; 6C fechada no frontend real.
O pack de blueprints da 6D foi validado numa worktree temporária; a aplicação manual
no projeto principal continua pendente (`QG6D-IMPL-001`). Packs em
`docs/blueprints/backend-files/sprint_6/` e
`docs/blueprints/frontend-files/sprint_6/`.

## Scope

Decisão backend-first de 2026-09-16: o layout aprovado (Claude Design) pressupõe
capacidades que o backend não tem. As fases 6A–6B fecham essas lacunas; o frontend
começa na 6C.

| Fase | Tipo | Resumo | Depende de |
|---|---|---|---|
| 6A | Backend — escrita e schema | RPE, porção padrão, check-in revisto, séries e concluir treino pelo cliente, tomas de suplementos; 1 migration | Gate 5D |
| 6B | Backend — leituras agregadas | Dashboard do trainer (com vendas de packs estimadas), resumo do cliente (adesão calculada), filtros, fila de moderação, treino de hoje | 6A |
| 6C | Frontend — Fundações | Vite + TS strict, tokens, AppShell com dropdown de perfil, cliente OpenAPI, sessão, seed, health, CI | 6B |
| 6D | Frontend — Admin | Visão geral, fila de moderação, catálogos globais e estado de vídeo de exercício | 6C |
| 6E | Frontend — Trainer | Dashboard, clientes, sessões/packs, planos, biblioteca, check-ins, definições, billing, vídeo 5D | 6C |
| 6F | Frontend — Cliente | Treino de hoje com registo, nutrição, suplementos com tomas, check-ins, perfil, white-label | 6C |
| 6G | Frontend — Auth UX | Login final, signup, email, password, convite, Google, Page 404 Not Found personalizada| 6C |

Fora: categoria de alimento e notas de moderação (excluídos); CSV, notificações in-app e
pesquisa transversal (DEF-PROD-004 a 006).

## Ordem de leitura

1. `.claude/memory/ACTIVE.md`
2. Este README
3. `docs/blueprints/plan_sprint_6_por_atualizar.md` (ponto de entrada local)
4. `.claude/project/frontend/layout/01_RELATORIO_ANALISE.md`
5. Documento de layout do ecrã da fase (`layout/02`–`06`) e a imagem correspondente em
   `layout/assets/` (PNG; não abrir o Claude Design nem PDF)
6. `.claude/project/02_SPRINTS_ROADMAP.md` §Sprint 6
7. Código só nos paths listados no blueprint da fase

## Gates

Definidos em `02_SPRINTS_ROADMAP.md` §Sprint 6 (Gate 6A a Gate 6G). Os IDs `QG6*-*`
são atribuídos no blueprint de cada fase e registados em `backlogs/QualityGates.md`.

## Decisões abertas

Decisões 1–4 do `frontend/layout/01_RELATORIO_ANALISE.md` §8 **fechadas na 6A** (ver
`sprint_6A/00`); decisões 5–8 **fechadas na 6B** em 2026-09-18 (ver `sprint_6B/00`, D1–D15):
adesão de 28 dias sobre o plano ativo, limiares de packs e planos, `TargetDate` fora do
contrato de prazo, top-5 por bloco no fuso do trainer, filtro `status=unreviewed` e treino de
hoje com semana cíclica. Nenhuma decisão de backend em aberto para o Sprint 6.

## Blockers

- Screenshots em `.claude/project/frontend/layout/assets/` (6 PNG, 12 de 13 artboards);
  falta só o 08 Command palette (não bloqueia; há especificação textual).
- Assets finais do logo e favicons (Codex) antes da 6C.
