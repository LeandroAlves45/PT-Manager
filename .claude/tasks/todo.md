# TODO — Sessão 2026-09-29 · Blueprints da fatia 6E-3 (biblioteca privada)

Objetivo: blueprints de código real validados da 6E-3 (biblioteca do trainer: exercícios com vídeo
5D, alimentos, suplementos), extraídos de uma worktree descartável `C:\ptm-tmp-sprint-6e3`
(branch `tmp/sprint-6e3`). Nunca merge; implementação real é do Leandro. Plano:
`00_plano_fase_6E_completa.md` §5.3 + plano aprovado
(`~/.claude/plans/c-users-leandro-alves-desktop-projeto-p-ethereal-breeze.md`).

## Decisões (fechadas com o Leandro)

- F1 `?tab=` + `search`/`activity`/`page` no URL · F2 globais só "Ver" · F3 `features/exercise-video/`
  com `catalog` · F4 `has_ready_video`/`managed_video_status` no `ExerciseResponse` · F5
  `MuscleGroupPicker` no trainer e no admin · F6 pesquisa/paginação no servidor · F7 arquivado = só
  leitura + Reativar · F8 sem comboboxes de catálogo · F9 bloqueado = badge + motivo, só leitura ·
  F10 `video_url` escondido e preservado · F11 dificuldade texto livre · U4 sem dependências novas.

## Checklist

- [x] `git status` do repo real registado (antes)
- [x] Worktree `C:\ptm-tmp-sprint-6e3` criada a partir de `085882f`
- [x] Backend F4 (DTO, response, projeção sem `IgnoreQueryFilters`, releitura no update, testes)
- [x] Backend `dotnet build -c Release` + `dotnet test` verdes (PostgreSQL descartável)
- [x] `schema.d.ts` regenerado + `api:types:check`
- [x] Frontend 03 vídeo partilhado (`features/exercise-video`)
- [x] Frontend 04 grupos musculares (picker + admin)
- [x] Frontend 05 biblioteca (`features/library`)
- [x] Frontend 06 rota
- [x] Frontend 07 testes em `src/test/`
- [x] lint, typecheck, vitest (`--maxWorkers=4`), build, Prettier nos tocados, greps de proibição
- [x] Mutações frontend + backend, todas mortas por teste nomeado
- [x] Revisão por subagente sonnet; defeitos corrigidos e registados em GOTCHAS
- [x] Extração dos docs 01–08 + leitor independente (0 falhas) + SHA-256
- [x] Worktree, branch e contentor apagados; `git status` do repo real (depois)
- [x] Memória: ACTIVE, MEMORY, NEST, Sessions, doc 00 §3/§5.3/§8, lessons, QualityGates

## Review

Finalizado. 8 docs em `sprint_6E3/`, 49 caminhos (39 completos, 5 em excerto, 5 a apagar), 0 falhas
no leitor independente. Backend 2902 verdes (+2), frontend 215/26 verdes, 31/31 mutações. 6 defeitos
apanhados antes da entrega (X1–X6, em `GOTCHAS_FRONTEND.md`). Repo real: só `.claude/`, `docs/`,
`backlogs/` alterados; worktree, branch e contentor apagados.

Pendente (Leandro): aplicar 02–07 num só commit e `npm run test:run` antes do push; verificações
manuais com login (isolamento dos dois trainers, visual), R2 real, `design-is`.
