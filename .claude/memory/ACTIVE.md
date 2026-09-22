# Estado ativo: Sprint 6C com blueprints validados

Atualizado: 2026-09-22

## Sprint 6C — frontend, blueprints validados

Pack `docs/blueprints/frontend-files/sprint_6/sprint_6C/` (00–18).
Materializado e validado numa worktree temporária já apagada; **nada implementado no
repositório real**.

- Backend na materialização: **3016 aprovados, 0 falhas, 1 skip**.
- Frontend novo: lint, typecheck, 26 testes e build verdes; **8/8 mutações mortas**.
- Contrato OpenAPI: 73 → **183 schemas**.
- Seed de desenvolvimento: ambiente completo, idempotente, login real 200 OK.

**Decisões:** D1 frontend do zero · D2 TypeScript 5.9.3 · D3 materialização temporária
apagada · D4 seed, health e CI no mesmo pack · D5 testes em inglês, UI/comentários em
PT-PT · D6 backend declara o tipo de todas as respostas · D7 do `.gitignore`, versiona-se
só o snapshot do contrato.

**Ordem de aplicação (não é a numérica):** 12 → 14 → 13 → 01 → 02 → 03 → 06 → 04 → 05 → 07
→ 08 → 09 → 10 → 11 → 15 → 16 → 17. Detalhe no doc 18 §4.

Ver `Sessions/2026-09-20-sprint6c-blueprints.md`.

## Próximo passo

Aplicar o pack e fechar `QG6C-IMPL-001`. Ficam a depender de verificação manual
`QG6C-SESSAO-001`, `QG6C-SHELL-001` e `QG6C-HTTPS-001` (browser) e `QG6C-CI-001`.

## Onde está cada documento

`.claude/memory/NEST.md` — índice único e método permanente de materialização.

## Histórico recente (não reler aqui)

- Sprint 6B fechada no backend real: `Sessions/2026-09-20-sprint6b-fecho-implementacao.md`
- Sprint 6A fechada: `Sessions/2026-09-17-sprint6a-fecho-implementacao.md`
- Sprint 5D (vídeo R2): `Sessions/2026-09-16-sprint5d-fecho-implementacao.md`

## Nota 2026-09-22 — Cursor lento

Hooks do projeto só correm com `/mem-on` / «liga a memória». Globais em
`~/.cursor/hooks.json` ficam vazios. Superpowers: plugin desligado; pedir com
`/superpowers`.
