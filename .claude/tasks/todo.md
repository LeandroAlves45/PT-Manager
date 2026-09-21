# TODO — Sessão 2026-09-20 · Sprint 6C (blueprints do frontend)

Plano aprovado: `C:\Users\Leandro Alves\.claude\plans\c-users-leandro-alves-desktop-projeto-p-tidy-gadget.md`.
Esta sessão **não implementa** código real. Produz blueprints validados numa pasta temporária.

## Decisões fechadas

- **D1** `frontend/` recomeça do zero (já esvaziado pelo utilizador, sem commit).
- **D2** TypeScript **6.0.3** — `typescript-eslint@8.70.0` exige `>=4.8.4 <6.1.0`; TS 7.0.2 mata o lint com tipos.
- **D3** Materialização em worktree **temporária**, apagada no fim. Nunca permanente, nunca merge.
- **D4** Seed, health checks e CI (C#) ficam no mesmo pack 6C.
- **D5** Identificadores, ficheiros, commits e nomes de teste (`describe`/`it`) em **inglês**;
  texto visível ao utilizador, comentários e JSDoc em **PT-PT**.

## 0. Base documental

- [x] `.claude/memory/NEST.md` — índice único de documentação + método de materialização
- [x] Ponteiro para o NEST em `.claude/memory/MEMORY.md`
- [x] `.claude/memory/Patterns/blueprints_codigo_real_frontend.md` — padrão adaptado ao frontend
- [x] Correções em `.claude/project/frontend/02_CONVENCOES.md` (§8 idiomas, §9 caminho, §10 branches)
- [x] `docs/blueprints/plan_sprint_6_por_atualizar.md` — 6B fechada, 6C a arrancar

## 1. Worktree temporária

- [x] `git worktree add C:\ptm-tmp-6c -b tmp/blueprints-6c`
- [x] Apagar `frontend/` dentro da worktree (vem de HEAD com o frontend antigo)
- [x] `git status` no repo real antes: registado

## 2. Sub-lote 6C-1 — projeto e estilos

- [x] `00_desenho_aprovado_indice_dependencias_gates.md`
- [x] `01_auditoria_dependencias_e_package_json.md`
- [x] `02_configuracao_projeto.md`
- [x] `03_tokens_tipografia_e_estilos.md`

## 3. Sub-lote 6C-2 — contrato e shared

- [x] `04_cliente_http_e_sessao.md`
- [x] `05_shared_lib_config_hooks.md`
- [x] `06_shared_ui_shadcn.md`
- [x] `07_shared_componentes_transversais.md`

## 4. Sub-lote 6C-3 — app, shell e auth mínima

- [x] `08_providers_router_guards.md`
- [x] `09_layouts_e_appshell.md`
- [x] `10_feature_auth_minima.md`
- [x] `11_testes_frontend_e_msw.md`

## 5. Sub-lote 6C-4 — backend de suporte e CI

- [x] `12_backend_seed_desenvolvimento.md`
- [x] `13_backend_health_e_logs_json.md`
- [x] `14_https_local_e_cors.md`
- [x] `15_ci_minimo.md`
- [x] `16_quality_gates_6C.md`
- [x] `17_rastreabilidade_e_relatorio.md`

## 6. Validação

- [x] `npm ci` · `lint` · `typecheck` · `test --run` · `build` verdes na worktree
- [x] `schema.d.ts` gerado do OpenAPI idêntico ao do blueprint
- [x] Backend `dotnet build -c Release` 0/0 + suite verde; seed idempotente (duas corridas)
- [x] Teste dos dois separadores: um só refresh
- [x] Greps de proibições: sem `localStorage` de tokens, sem `axios`, sem `fetch(` fora do cliente, sem hex no JSX
- [x] Diff programático normalizado blueprint ↔ ficheiro materializado
- [x] `git status` no repo real: só `docs/`, `.claude/` e `backlogs/`

## 7. Fecho

- [x] `git worktree remove --force C:\ptm-tmp-6c` + `git branch -D tmp/blueprints-6c`
- [x] `.claude/memory/Sessions/2026-09-20-sprint6c-blueprints.md`
- [x] `ACTIVE.md` atualizado
- [x] `backlogs/QualityGates.md` com os `QG6C-*`

## Review

**Finalizado** — 2026-09-20.

Entregue: pack `docs/blueprints/frontend-files/sprint_6/sprint_6C/` com 19 documentos,
6523 linhas e 77 blocos de código, todos verificados byte a byte contra os ficheiros
validados. Mais `.claude/memory/NEST.md`, o padrão de blueprints de frontend e quatro
correcções documentais.

Evidência: backend 3016 testes verdes (0 falhas, 1 skip); frontend com lint, typecheck,
26 testes e build verdes; 8/8 mutações mortas; contrato de 73 para 183 schemas; seed
idempotente com login real a devolver 200.

Desvios ao plano aprovado, todos decididos com o utilizador durante a sessão:

- **TypeScript 5.9.3 em vez de 6.0.3.** O plano fixava 6.0.3 com base no
  `typescript-eslint`. Ao instalar, o `openapi-typescript@7.13.0` recusou
  (`peerDependencies: ^5.x`). A intersecção real das duas restrições é a linha 5.9.
- **Doc 12 acrescentado ao pack** (contrato OpenAPI tipado). Não estava previsto: só
  apareceu ao gerar os tipos e ver 169 de 169 respostas sem corpo. Sem ele, metade da fase
  não teria sentido.
- **Pack com 19 documentos em vez de 18**, por causa do acima.
- **`.gitignore`**: aplicada só a excepção do snapshot do contrato, conforme escolha do
  utilizador. Os blueprints continuam fora do git.

Por fazer: aplicar o pack (`QG6C-IMPL-001`), verificar três gates no browser e correr o CI
pela primeira vez.