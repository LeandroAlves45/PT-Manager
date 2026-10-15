# Sprint Pack — Sprint 6 (backend do layout + frontend)

Estado em 2026-10-14: 6A e 6B fechadas no backend; 6C, 6D, 6E e 6F fechadas no frontend
(6F em `503d113`, CI #39 verde). O 6G divide-se em **6GA** (Auth UX, blueprints validados,
aplicação pendente) e **6GB** (análise visual e correção, registada; prints do portal por
entregar). Plano do 6G em `sprint_6G/00_plano_sprint_6G.md`. Packs em
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
| 6F | Frontend — Cliente | 4 fases: 6F-1 casa e marca (white-label AA, Início), 6F-2 treino (+ vídeo, backend aditivo), 6F-3 nutrição e suplementos, 6F-4 check-ins e perfil | 6C |
| 6GA | Frontend + ajustes de backend — Auth UX | Login final (`/entrar`), signup sem enumeração, confirmação e reenvio, recuperar/repor/mudar password, convite com palavra-passe ou Google, Google (entrar, criar, ligar), 404 personalizada, "Conta e segurança" | 6C |
| 6GB | Frontend — Análise visual e correção | Revisão visual de admin, trainer, cliente e auth a partir dos prints do Leandro; correção dos P1/P2 (`sprint_6G/00` §4) | 6F, 6GA |

Fora: categoria de alimento e notas de moderação (excluídos); CSV, notificações in-app e
pesquisa transversal (DEF-PROD-004 a 006).

## Ordem de leitura

1. `.claude/memory/ACTIVE.md`
2. Este README
3. `docs/blueprints/frontend-files/sprint_6/plan_sprint_6_por_atualizar.md` (ponto de entrada local)
   e o `00` da fase ativa (`sprint_6G/00_plano_sprint_6G.md`; para aplicar a 6GA,
   `sprint_6G/sprint_6GA/01`)
4. `.claude/project/frontend/layout/01_RELATORIO_ANALISE.md`
5. Documento de layout do ecrã da fase (`layout/02`–`06`) e a imagem correspondente em
   `layout/assets/` (PNG; não abrir o Claude Design nem PDF)
6. `.claude/project/02_SPRINTS_ROADMAP.md` §Sprint 6
7. Código só nos paths listados no blueprint da fase

## Gates

Definidos em `02_SPRINTS_ROADMAP.md` §Sprint 6 (Gate 6A a Gate 6F, Gate 6GA e Gate 6GB). Os IDs `QG6*-*`
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
