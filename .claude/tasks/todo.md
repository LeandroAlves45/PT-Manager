# TODO: sessão de 2026-10-11 · Sprint 6F-3 (Nutrição e suplementos do portal), blueprints

Plano aprovado em `~/.claude/plans/c-users-leandro-alves-desktop-projeto-p-wise-melody.md`.
Só documentação: `frontend/` e `backend/` do repo real não mudaram. Worktree `C:\ptm-tmp-6f3`
(branch `tmp/blueprints-6f3`), apagada no fim. Um processo pesado de cada vez.

## 1. Preparação
- [x] Ler NEST, ACTIVE, lessons, GOTCHAS_FRONTEND, pack 6F-2 (formato), layout 05
- [x] Confirmar contratos no backend (nutrição, tomas, códigos de erro)
- [x] Decisões U16–U19 com o Leandro
- [x] `git status` + criar worktree
- [x] `npm ci` na worktree + `npm audit` (só o `source-map-js` já conhecido)

## 2. Implementação na worktree
- [x] Fixtures e handlers MSW
- [x] `api/portal.ts` (keys, queries, toggle da toma)
- [x] `lib/nutrition.ts` + teste
- [x] Componentes (`MealCard`, `SupplementIntakeRow`)
- [x] `PortalNutritionPage` + teste
- [x] `PortalSupplementsPage` + teste
- [x] `index.ts` e `router.tsx`

## 3. Validação
- [x] Vitest por ficheiro (14 + 10 + 13), typecheck, lint
- [x] Suite completa 419/419 (`--maxWorkers=2`) e build
- [x] Verificação visual no browser (arnês MSW descartável; 375 px, claro/escuro)
- [x] Mutações M01–M34: 34 mortas
- [x] Greps de proibição e Prettier dos 13 ficheiros

## 4. Pack
- [x] Docs `sprint_6F3/01–06` + `manifest.json`
- [x] Round-trip programático 13/13
- [x] `00_plano_fase_6F_completa.md`, `10_fecho_sprint_6F.md`, layout 05, NEST, QualityGates

## 5. Fecho
- [x] Entrada temporária do `launch.json` removida; worktree e branch apagados
- [x] `git status` final: só `.claude/` (o `docs/` e o `backlogs/` estão no `.gitignore`)
- [x] Memória: ACTIVE, MEMORY, nota de sessão, lessons

## Review

- Entregue: pack 6F-3 com 13 caminhos (12 integrais, 1 patch), validado e reproduzível por
  SHA-256. Sem commit.
- Decisões: U16–U19. Desvios V1–V9 no doc 01.
- Não feito: auditoria `design-is` formal (ficaram as observações V7 e V8); pesquisa web de
  dependências (não foi precisa: zero pacotes novos e `npm audit` sem achados novos); o MCP
  Playwright não ligou, usou-se o browser integrado.
- Por fazer pelo Leandro: aplicar o pack, `npm run test:run` antes do push, gates manuais
  `QG6F3-UI-001` e `QG6F3-ISOLAMENTO-UI-001`.
- Para a 6F-4: largura máxima do portal em ecrãs largos (V8) e extrair o estado "Portal
  indisponível" (4.º uso).
