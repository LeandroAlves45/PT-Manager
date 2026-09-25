# Estado ativo: Sprint 6E — fatia 6E-1 FINALIZADA; próxima 6E-2

Atualizado: 2026-09-25

## Sprint 6E — frontend trainer

Pack `docs/blueprints/frontend-files/sprint_6/sprint_6E/`. **Ler o `00` antes de qualquer fatia**
(plano da fase inteira: fatias, contratos/limites/erros confirmados, inventário, armadilhas).

- **6E-1 FINALIZADA (2026-09-25)** no repo real: docs 02–07 aplicados (o 07 faltava e partiu o
  CI), 7 defeitos corrigidos, 143 testes, 17/17 mutações, design-is 21/30 REFINE; BD dev com as
  4 contas. Relatório `sprint_6E1/09`. Faltam só `QG6E1-ISOLAMENTO-UI-001` e `QG6E1-UI-001`
  (login manual).
- Próxima fatia a planear: 6E-2 (sessões e packs), doc 00 §5.2.
- Tarefa transversal sugerida: rotas lazy por papel (bundle único de 254 kB gzip).

Ver `Sessions/2026-09-25-sprint6e1-fecho-frontend.md`.

## Sprint 6D — fechada no repositório real

Pack `docs/blueprints/frontend-files/sprint_6/sprint_6D/` (00–07). Relatório:
`07_relatorio_fecho_fase_6D.md`. Commit `9319c2f` em `main`.

- 5 defeitos corrigidos na aplicação dos packs 02–03 (D1–D5).
- Gestão de vídeo do admin acrescentada no fecho (doc 06), só frontend, endpoints 5D.
- 112 testes em `frontend/src/test/`, 13/13 mutações, gates locais verdes; CI no relatório.
- Pendentes externos: inspeção manual com login real (`QG6D-ADMIN-001`) e R2 real
  (`QG6D-VIDEO-R2-001`, depende de `QG5D-PROVIDER-001`).

Ver `Sessions/2026-09-24-sprint6d-fecho-frontend.md`.

## Sprint 6C — fechada no repositório real

Pack `docs/blueprints/frontend-files/sprint_6/sprint_6C/` (00–19). Relatório de fecho:
`19_relatorio_fecho_fase_6C.md`. Commits em `main`: `b6e4ed0` (correções + testes),
`a9feff2` (CI), `7ef6700`; pushed.

- Frontend: 83 testes em 13 ficheiros em **`frontend/src/test/`** (espelha `src/`), 13/13
  mutações mortas, lint/typecheck/build/audit verdes após `npm ci`.
- 9 defeitos corrigidos (refresh obsoleto ressuscitava sessão, `/\host` no retorno do login,
  CommandMenu termo/resultados, `signOut` sem catch, `npx.cmd` EINVAL no check de tipos, …).
- CI real: GitHub Actions run #6 (`35879244010`) verde — Frontend, Backend, Contrato OpenAPI.
- Backend 6C (docs 12–15) inalterado desde 2026-09-21.

## Próximo passo

Verificação manual no browser pelo utilizador (exige login com password):
`QG6C-SHELL-001` (1440/768/375), `QG6C-HTTPS-001` (cookie `__Secure-`, restauro) e dois
separadores de `QG6C-SESSAO-001`. Depois, Sprint 6D.

Ver `Sessions/2026-09-23-sprint6c-fecho-frontend.md`.

## Onde está cada documento

`.claude/memory/NEST.md` — índice único e método permanente de materialização.

## Histórico recente (não reler aqui)

- Sprint 6C blueprints: `Sessions/2026-09-20-sprint6c-blueprints.md`; backend: `Sessions/2026-09-21-sprint6c-backend-testes.md`
- Sprint 6B fechada no backend real: `Sessions/2026-09-20-sprint6b-fecho-implementacao.md`
- Sprint 6A fechada: `Sessions/2026-09-17-sprint6a-fecho-implementacao.md`
- Sprint 5D (vídeo R2): `Sessions/2026-09-16-sprint5d-fecho-implementacao.md`

## Nota 2026-09-22 — Cursor lento

Hooks do projeto só correm com `/mem-on` / «liga a memória». Globais em
`~/.cursor/hooks.json` ficam vazios. Superpowers: plugin desligado; pedir com
`/superpowers`.
