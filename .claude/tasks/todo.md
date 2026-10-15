# TODO: sessão de 2026-10-14 · Plano do Sprint 6G (6GA + 6GB) e blueprints da 6GA

Plano aprovado em `~/.claude/plans/c-users-leandro-alves-desktop-projeto-p-jazzy-hearth.md`.
Base: `9341228` (main). Decisões U27–U38.

## 1. Análise e decisões
- [x] ACTIVE, NEST, README do Sprint 6, plano do Sprint 6, `sprint_6F/00` e `10`
- [x] Inventário auth frontend e backend (agentes sonnet), verificado no código
- [x] 19 prints da 6GB lidos e classificados
- [x] Decisões U27–U38 com o Leandro (mapa de autenticação explicado)

## 2. Documentação
- [x] `plan_sprint_6_por_atualizar.md` (6F fechada, 6G → 6GA + 6GB, §7, §8)
- [x] `sprint_6G/00_plano_sprint_6G.md` com a lista 6GB e a entrada reservada do portal
- [x] `.claude/project/sprints/sprint-6/README.md`
- [x] `02_SPRINTS_ROADMAP.md` (§6G, linhas soltas, CSP no Sprint 9 §4)
- [x] `ACTIVE.md`, `NEST.md`
- [x] `backlogs/QualityGates.md` (QG6GA-*, QG6GB-*)
- [x] Grep final sem "6G = Auth UX" solto

## 3. Worktree `C:\ptm-tmp-6ga` (sandbox, sem merge)
- [x] Criar a partir de `9341228`
- [x] Backend: E1 signup 202, E2 email-confirmation/request, E3 accept-invite/register, E4 account, U37 create_account, U32 email de dev, rate limits
- [x] Backend: testes unitários, integração e funcionais; snapshot 173; suite verde (3161 + 1)
- [x] Frontend: rotas PT, 404, client.ts 401 de domínio, features/auth, features/account, Google
- [x] Frontend: testes, lint, typecheck, format, build (700/700); `api:types:check` exige API local → CI
- [x] Mutações dirigidas (23/23 mortas)
- [x] Verificação visual com MSW (375/1440, claro/escuro) — D6 corrigido
- [x] security-reviewer sobre o diff (LOW-1 corrigido)

## 4. Blueprints `sprint_6G/sprint_6GA/`
- [x] Docs 01–11 + manifest.json
- [x] Round-trip programático 163/163
- [x] Worktree desregistada e branch apagada; a pasta `C:\ptm-tmp-6ga` é protegida e fica para o Leandro apagar

## 5. Fecho
- [x] obsidian-ptmanager: MEMORY.md, nota de sessão, memória auto
- [x] lessons / GOTCHAS (U+0000, 401 de domínio, flash, migalhas, NUL do node, autocrlf)
- [x] Review final nesta secção

## Review

- 6G dividido e documentado; 6GB com 19 pontos (P1: scroll de grupos musculares e atribuição
  da Ana em falta), portal reservado.
- 6GA: pack 01–11 + manifest com 163 caminhos e round-trip 163/163. Decisões U27–U38 e
  defeitos D1–D7. Backend 3161 + 1, Vitest 700, 23/23 mutações.
- Reparado: corrupção antiga no `MEMORY.md`/`ACTIVE.md` (texto "D1 `$`" trocado pelo
  cabeçalho) e dois NUL reais no `GOTCHAS_BACKEND.md`.
- Pendente do Leandro: apagar `C:\ptm-tmp-6ga`, aplicar o pack, correr o CI e os gates manuais
  `QG6GA-GATE-001..004`, e entregar os prints do portal para a 6GB.
