# TODO: Sprint 6GB · fase Trainer (6GB-TR)

Plano aprovado a 2026-10-21 em `~/.claude/plans/curious-herding-lampson.md`. Decisões U53–U67.
Branch `feat/6gb-trainer`, base `ebb6568` (main). Instruções completas em `.claude/SPEC.md`.

## 0. Arranque
- [x] Prints `trainer_*`, `docs_about_app`, `sprint_6G/00` §4.2, gotchas e lições lidos
- [x] Decisões U53–U67 com o Leandro
- [x] Branch, `todo.md`, `SPEC.md`
- [x] `sprint_6GB/02_plano_6GB_trainer.md`
- [x] Baseline: backend 3223 + 1 skip; frontend 791/791, lint e typecheck limpos
- [x] Ficheiros de teste sem sprint no nome (11 renomeados, pedido do Leandro)

## 1. Backend (TR-1) — 3264 + 1 skip, 0 avisos, format limpo
- [x] B1 repetições em intervalo (`PlannedRepsMax`) + migration `AddPlannedRepsRange` (aplicada, Down verificado)
- [x] B2 «gr» → «g» (`SupplementUnits`)
- [x] B3 convite devolve link e validade (200 `InviteClientResponse`)
- [x] B4 convite na ficha: `GET /clients/{id}/invitation` (endpoint próprio, para o PUT da ficha não o apagar)
- [x] B5 `GET /workout-completions` para o trainer
- [x] B6 testes + `schema.d.ts` regenerado + snapshot da superfície

## 2. Frontend (TR-2/TR-3)
- [x] F0 fundações: rotas lazy + fallback, tokens de macros AA, breadcrumbs, redirecionamentos
- [x] F1 marca do trainer na shell (nome, logo, cor, topbar, título)
- [x] F2 painel com destaque dinâmico
- [x] F3 clientes em páginas, cabeçalho, convite, validade
- [x] F4 sessões e packs em páginas
- [x] F5 avaliações em páginas
- [x] F6 planos de treino em passos, reps mín/máx, séries e nota (dia no fuso do personal trainer)
- [x] F7 planos alimentares em passos com restante e macros
- [x] F8 suplementos atribuídos (ponto 19: dados reais mostram que o suplemento da Ana está numa refeição do plano «Fase de corte», não é defeito da lista; linha de ajuda na lista)
- [x] F9 biblioteca em páginas, vídeo ao criar
- [x] F10 marca própria: fuso, cor, logo

## 3. Verificação e fecho (TR-4)
- [x] Backend Release + format (3266 + 1 skip, 0 avisos); frontend lint/typecheck/Prettier/test/build; api:types:check verde
- [x] Mutações 10/10 mortas: B intervalo de reps, B fronteira \b do gr, B gr só minúsculas, B validade exata do convite, F destaque a zero, F restante nunca negativo, F contribuição /100, F ordem «12-10», F fuso do personal trainer nas séries, F nome da marca na shell
- [x] Segurança (agente sonnet): sem achados
- [x] Browser 375/1440, claro/escuro (arnês MSW; 28 rotas sem overflow; 3 defeitos corrigidos: nome da marca cortado na sidebar, setas de scroll nos separadores, grelha do painel a 375 px)
- [x] Revisão sonnet (`code-review-leandro`: 2 médias + 8 baixas tratadas; email falhado → `email_sent: false`, decisão do Leandro), `security-reviewer` (sem achados), `design-is` (20/30 REFINE, 4 de 5 corrigidos)
- [x] Relatório `03`, docs_about_app (com autorização), GOTCHAS/lessons, ACTIVE, sessão, memória
- [ ] Apagar `SPEC.md`, commits, checklist «Finalizado»

## Review
(por preencher no fecho)
