# TODO: Sprint 6GB · fase do Cliente (6GB-CL)

Plano aprovado a 2026-10-22 em `~/.claude/plans/dapper-swimming-dove.md`. Decisões U68–U83.
Branch `feat/6gb-client`, base `8a65094` (main). Resumo em
`docs/blueprints/frontend-files/sprint_6/sprint_6G/sprint_6GB/04_plano_6GB_cliente.md`; fecho em `05_relatorio_fecho_6GB_cliente.md`.

## 0. Arranque (CL-0)
- [x] Prints do cliente, `docs_about_app`, `sprint_6G/00` §4.3, gotchas e lições lidos
- [x] Decisões U68–U83 com o Leandro
- [x] Branch, `todo.md`, `SPEC.md`, `04_plano_6GB_cliente.md`
- [x] Baseline: backend 3266 + 1 skip; frontend 900/900 em 85 ficheiros

## 1. Backend (CL-1) — `7ae35dd`
- [x] B1 plano alimentar escolhido: `Client.SelectedMealPlanId` + migration `AddClientSelectedMealPlan` (aplicada, `Down` verificado)
- [x] B1 `GET /portal/my-meal-plans` e `PUT /portal/my-meal-plans/selection`
- [x] B1 `my-nutrition` e home leem a escolha; arquivar (e reativar) limpa a escolha
- [x] B2 `GET /portal/my-workout/days` e `GET /portal/my-workout/day?date=`
- [x] B2 notas das séries e nota de conclusão no treino do portal
- [x] B3 perfil: nome obrigatório, sem email de contacto (telefone continua obrigatório, U83)
- [x] B4 testes unitários e funcionais, `schema.d.ts`, snapshot da superfície

## 2. Fundações do frontend (CL-2) — `5b4e38f`
- [x] Rotas lazy do portal + pré-carregamento nos testes
- [x] `useDocumentTitle` partilhado no portal
- [x] Cabeçalho: nome da marca em 2 linhas, sem cortes
- [x] Coluna larga no desktop, fundo com a cor da marca
- [x] Tokens de secção (AA, claro/escuro) + cópia para `site/`
- [x] `PortalCard` partilhado
- [x] Textos «Avaliações» no portal

## 3. Páginas (CL-3) — `5b4e38f`
- [x] Treino: progresso, filtro de dia, plano recolhível
- [x] Nutrição: lista + escolha, barras das macros, chips, «Indisponível», sem link de tomas
- [x] Perfil, Avaliações e Suplementos: nome sem email, «Dados guardados», avaliação já não pendente, motivo do bloqueio
- [x] Início: cores por secção, progresso, «Escolhe o teu plano»
- [x] MSW handlers e fixtures

## 4. Verificação (CL-4) — `5a63fc4`, `16cd359`
- [x] Vitest 952/952 (88 ficheiros), lint, typecheck, Prettier, build, `api:types:check`; `site/` 44/44
- [x] Backend 3298 + 1 skip, 0 avisos, format limpo
- [x] Mutações 14/14 mortas
- [x] Browser: arnês MSW (375/1440, claro e escuro) + app real no Chrome (Marta e João)
- [x] Gates `QG6F4-ISOLAMENTO-UI-001` e `QG6F-GATE-001` passam; `QG6F4-UI-001` e `QG6F4-AVATAR-001` ficam com o Leandro
- [x] `code-review-leandro` (0 altas, 3 médias corrigidas), `security-reviewer` (sem achados)
- [ ] `design-is`

## 5. Fecho (CL-4)
- [x] Relatório `05_relatorio_fecho_6GB_cliente.md`
- [x] `docs_about_app` atualizado (anunciado antes)
- [x] `sprint_6G/00` §4.3, `QG6GB-LISTA-001`, `QG6GB-GATE-001`
- [x] Gotchas e lições
- [ ] Graphify regenerado (fecho do Sprint 6)
- [ ] Memória: `ACTIVE.md`, `Sessions/`, `MEMORY.md`
- [ ] Commit da documentação; apagar `SPEC.md`; checklist «Finalizado»

## Review
- Entregue: U68–U83 (portal por secções e cores, escolha do plano alimentar, filtro de dia, perfil
  com nome, «Avaliação periódica», rotas lazy, marca no separador e no cabeçalho).
- Defeitos encontrados e corrigidos: 4 da implementação (testes de migrations, mutação mal apontada,
  lint do perfil, foco só no jsdom) e 7 da revisão. Detalhe no §3 do relatório.
- Pendente documentado (RN-014): suplementos fora de refeição e tomas por plano, para o bloco seguinte.
