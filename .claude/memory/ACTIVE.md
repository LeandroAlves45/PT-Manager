# Estado ativo: Sprint 6F (portal do cliente). 6F-1 e 6F-2 FINALIZADAS, 6F-3 por gerar

Atualizado: 2026-10-10

## Sprint 6F: portal do cliente

**Ler primeiro** `docs/blueprints/frontend-files/sprint_6/sprint_6F/00_plano_fase_6F_completa.md`.
Tem as fases, as decisões U1–U15, os contratos de todas as fases e as armadilhas.

- **6F-2 FINALIZADA (2026-10-10).**
  - Aplicação do Leandro em `f0c827b` (backend) e `41c02a9` (frontend, 12 testes vermelhos).
  - Fecho em `5310809` (CI #31 verde): D1 decimais do plano, D2 `formatDecimal` partia as
    repetições, D3 códigos de validação em `errors[]`, D4–D7 (vídeo, diálogo, `aria-valuenow`,
    copy).
  - 382 testes, 33/33 mutações, blueprints realinhados.
  - Relatório `sprint_6F2/09_relatorio_fecho_fase_6F2.md`.
  - Ver `Sessions/2026-10-10-sprint6f2-fecho-frontend.md`.
  - Faltam os gates manuais `QG6F2-UI-001`, `QG6F2-ISOLAMENTO-UI-001`, `QG6F2-VIDEO-001`,
    `QG6F2-PESO-001` e o `QG6F2-AUDIT-001`.
  - **Próximo passo:** blueprints da 6F-3 (nutrição, suplementos e tomas de hoje).
- **6F-2 blueprints validados (2026-10-09, histórico).**
  - Pack: `sprint_6F/sprint_6F2/01–08` + `manifest.json`, com 38 caminhos (32 integrais e 6
    patches).
  - Âmbito:
    - treino de hoje (registar, corrigir, desmarcar, concluir com notas);
    - plano só de leitura;
    - vídeo pelo `exercise_id`;
    - backend `exercise_id` + `has_ready_video`;
    - peso máximo de 1000 kg (U15, corrigia um 500).
  - Evidência na worktree `C:\ptm-tmp-6f2` (apagada):
    - backend 2932 + 1 skip;
    - Vitest 372;
    - 27 mutações mortas e 1 equivalente;
    - round-trip 38/38.
  - Ver `Sessions/2026-10-09-sprint6f2-blueprints.md`.

- **6F-1 FINALIZADA (2026-10-08).**
  - Fecho do frontend em `37e9acd` (CI #27 verde): D1 mistura de cores, D2 rota do "Responder", D3
    `testid` do patch, D4 Prettier.
  - 319 testes e teste novo de navegação dos cartões.
  - Relatório `sprint_6F1/09_relatorio_fecho_fase_6F1.md`.
  - Ver `Sessions/2026-10-08-sprint6f1-fecho-frontend.md`.
  - Faltam `QG6F1-UI-001` e `QG6F1-ISOLAMENTO-UI-001` (login manual).
- **6F-1 blueprints (2026-10-04, histórico).**
  - Pack: `sprint_6F/sprint_6F1/01–08` + `manifest.json`, com 31 caminhos (24 integrais e 7
    patches `git apply --recount`).
  - Âmbito:
    - marca com tom AA por tema;
    - `body_color` só no cabeçalho;
    - monograma e logo PT Manager;
    - 5 itens na barra e Início com 4 cartões;
    - pré-visualização da 6E-5 alinhada;
    - seed (João, marca, `cliente2@`).
  - Evidência na worktree `C:\ptm-tmp-6f` (apagada):
    - 2920 testes backend e 315 frontend;
    - 24/24 mutações mortas;
    - round-trip 31/31.
  - Ver `Sessions/2026-10-04-sprint6f-plano-6f1-blueprints.md`.
- Gates manuais da 6F-1 (doc 08) continuam com o Leandro (o reset foi feito a 2026-10-07).
- 6F-3 e 6F-4 estão por gerar. O fecho do sprint fica em `sprint_6F/10_fecho_sprint_6F.md`.

## Sprint 6E — frontend trainer

Pack `docs/blueprints/frontend-files/sprint_6/sprint_6E/`. **Ler o `00` antes de qualquer fatia**
(plano da fase inteira: fatias, contratos/limites/erros confirmados, inventário, armadilhas).

- **6E-1 FINALIZADA (2026-09-25)** no repo real: docs 02–07 aplicados (o 07 faltava e partiu o
  CI), 7 defeitos corrigidos, 143 testes, 17/17 mutações, design-is 21/30 REFINE; BD dev com as
  4 contas. Relatório `sprint_6E1/09`. Faltam só `QG6E1-ISOLAMENTO-UI-001` e `QG6E1-UI-001`
  (login manual).
- **6E-2 FINALIZADA (2026-09-29)** no repo real: docs 02–06 do Leandro (`4eb1831`, `d33d68e`) e
  doc 07 + correções no fecho (`597eb19`). 4 defeitos corrigidos (rótulo, submit antes dos packs,
  fim previsto sugerido, fuso dos testes fixo em `Europe/Lisbon`), 184 testes frontend, 29/29
  mutações (+1 equivalente), backend 2900. Relatório `sprint_6E2/09`. Faltam
  `QG6E2-ISOLAMENTO-UI-001` e `QG6E2-UI-001` (login manual) e o `design-is`.
  Ver `Sessions/2026-09-29-sprint6e2-fecho-frontend.md`.
- **6E-3 FINALIZADA (2026-09-30)** no repo real: docs 02–06 do Leandro (`5cd1d7f`) e doc 07 +
  6 correções no fecho (D1 "Com vídeo" só ícone, D2 `wrap-break-words` inexistente, D3 copy "!",
  D4 soma dos macros em vírgula flutuante, D5 lista não acompanhava o vídeo com o painel fechado →
  polling enquanto houver vídeo em processamento, D6 `Object.hasOwn`). 222 testes frontend,
  M1–M27 27/27 + 6/6 novas, backend 2902. Relatório `sprint_6E3/09`. Faltam
  `QG6E3-ISOLAMENTO-UI-001`, `QG6E3-UI-001`, `QG6E3-VIDEO-R2-001`, `QG6E3-DESIGN-001`.
  Ver `Sessions/2026-09-30-sprint6e3-fecho-frontend.md`.
- **6E-4 BLUEPRINTS VALIDADOS (2026-10-01)**: pack `sprint_6E/sprint_6E4/01–09` cobre planos de treino, séries, planos alimentares com preview e suplementos. A implementação foi testada numa worktree descartável, sem integração no projeto principal. O backend requer adições `Preserve`: `has_history`, `client_name` em listas e campos do cálculo para repor o editor. Ver `Sessions/2026-10-01-sprint6e4-blueprints.md`. Aplicação real, CI e verificação visual com login continuam pendentes.
- **6E-4 BACKEND APLICADO E TESTADO (2026-10-01)**: o backend do doc 02 foi aplicado, com M1–M3. São 2911 testes, MU1–MU6 6/6 e o contrato foi regenerado. Os blueprints frontend 03–08 foram corrigidos (A1, B3, B5–B8) e validados numa worktree (234 testes, FM1–FM3 3/3, 46/46 hashes). Próximo passo: o Leandro aplica o frontend e fecha a fase. Os patches do pack exigem `git apply --recount`. Ver `Sessions/2026-10-01-sprint6e4-revisao-backend-testes.md`.
- **6E-4 FINALIZADA (2026-10-02)**: docs 03–08 do Leandro em `88d3dfa`; revisão de fecho em `e6c5c4e` com D1 (histórico a meio da edição repõe datas/estrutura), D2 (aviso de carregamento eterno), D3 (reativação após 409 passa a edição). 236 testes frontend, FM4–FM9 6/6. Relatório `sprint_6E4/10`. Faltam `QG6E4-ISOLAMENTO-UI-001` e `QG6E4-UI-001`. Ver `Sessions/2026-10-02-sprint6e4-fecho-frontend.md`.
- **6E-5 BLUEPRINTS VALIDADOS (2026-10-02)**: pack `sprint_6E/sprint_6E5/01–08` + `manifest.json` (55 caminhos, 47 integrais, 8 patches `git apply --recount`). Check-ins (agendar, reagendar, cancelar, rever, corrigir), definições (marca com pré-visualização, logo multipart, contactos, fuso), subscrição (Checkout/portal com `Idempotency-Key`, aviso de regresso e polling) e tab Check-ins. Backend: `client_name` em `CheckInResponse` e retoma do Checkout aberto do mesmo plano (U5). D1: `SubscriptionCard` mostrava sempre "Pagamento por regularizar". Worktree: 2917 testes backend, 277 frontend, 15/15 mutações, round-trip 55/55. **Próximo passo:** o Leandro aplica o pack (ordem no doc 01), configura as URLs de retorno Stripe e faz os gates manuais do doc 08 (incluindo o gate global da 6E). Ver `Sessions/2026-10-02-sprint6e5-blueprints.md`.
- **6E-5 FINALIZADA (2026-10-04)**:
  - O pack foi aplicado pelo Leandro em `3c5382f`; o CI #24 falhou nos testes do Frontend (12/277).
  - O fecho corrigiu:
    - D1: a lista enviava `page` em vez de `page_number`, e o `tsc` não apanhou;
    - D2: link `?tab=checkins` contra a tab `check-ins`;
    - D3: o diálogo do logo não fechava;
    - C1–C4: copy;
    - CR1: página vazia sem paginação depois de rever;
    - CR2: erro do cliente num campo escondido;
    - CR4;
    - P1: polling sem limite com o servidor em baixo.
  - Resultado: 282 testes, 19/19 mutações, blueprints realinhados (`final_sha256`).
  - Relatório `sprint_6E5/09`; fecho do sprint em `sprint_6E/10_fecho_sprint_6E.md`.
  - Faltam os gates manuais `QG6E5-*` e `QG6E-GATE-001`.
  - Ver `Sessions/2026-10-04-sprint6e5-fecho-sprint6e.md`.
- **Próximo passo:**
  - gates manuais com login da 6E (lista em `sprint_6E/10`, secção 3);
  - depois, planear a Sprint 6F (portal do cliente);
  - regenerar o graphify no fecho do Sprint 6 inteiro (6G), não agora.
- Tarefa transversal registada: pesquisa na página N faz um pedido intermédio (termo antigo,
  página 1) — biblioteca e `ModerationPage`.
- Tarefa transversal sugerida: rotas lazy por papel (bundle único de 266 kB gzip em 2026-09-29).

Ver `Sessions/2026-09-29-sprint6e2-fecho-frontend.md` (6E-2) e
`Sessions/2026-09-25-sprint6e1-fecho-frontend.md` (6E-1).

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
