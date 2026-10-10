# TODO: sessão de 2026-10-09 · Sprint 6F-2 (Treino do portal), blueprints

Plano aprovado em `~/.claude/plans/docs-blueprints-frontend-files-sprint-6-glittery-lemur.md`.
Decisões U11–U15 no `00` da 6F §2 e em `sprint_6F2/01`. Worktree `C:\ptm-tmp-6f2`, branch
`tmp/blueprints-6f2`, base `0c24109` (ambas apagadas).

## 0. Arranque
- [x] `git status` limpo no repo real (`0c24109`)
- [x] Worktree criada
- [x] `npm ci` na worktree

## 1. Backend aditivo
- [x] DTOs (`MyTrainingPlanDto`, `MyWorkoutTodayDto`) com `ExerciseId` e `HasReadyVideo`
- [x] `MyTrainingPlanQueries` (regra de vídeo do cliente) e `MyWorkoutTodayReader`
- [x] Contratos `MyDayExerciseResponse` e `MyWorkoutExerciseResponse`
- [x] Testes unitários, de integração e funcionais
- [x] Precisão de `weight_kg` confirmada: dava 500. Corrigido com U15 (máximo 1000 kg)
- [x] Build Release, suites, `dotnet format`
- [x] `schema.d.ts` regenerado, `api:types:check` verde

## 2. Frontend
- [x] `portal.ts` (queries e mutations), `lib/workout.ts`
- [x] Componentes (linha de série, cartão, diálogo de conclusão, vídeo, erro do plano)
- [x] `PortalTodayPage`, `PortalPlanPage`, `index.ts`, `router.tsx`
- [x] Patch `exerciseVideo.ts` (audiência `client`)
- [x] Testes, fixtures e handlers MSW
- [x] Lint, typecheck, testes, build, Prettier
- [x] Mutações: 27 mortas e 1 equivalente (BM2)

## 3. Pack `sprint_6F2/`
- [x] Docs 01–08 + `manifest.json`
- [x] Round-trip SHA-256 38/38

## 4. Fecho
- [x] Worktree e branch apagados; `git status` só com `.claude/` (o `docs/` é ignorado pelo git)
- [x] Memória (nota de sessão, ACTIVE, MEMORY, NEST), `00` §2/§8, `10_fecho`, `QualityGates.md`, `lessons.md`, `GOTCHAS_BACKEND.md`
- [x] Review nesta página

## Review

**Finalizado.**

- **Pack:** 38 caminhos (32 integrais e 6 patches):
  - doc 02: contrato e vídeo;
  - doc 03: peso máximo;
  - docs 04–06: frontend;
  - doc 07: testes;
  - doc 08: gates.
- **Evidência na worktree:**
  - backend 2932 verdes + 1 skip;
  - Vitest 372/372;
  - build com 301,06 kB gzip;
  - contrato só aditivo;
  - 27 mutações mortas e 1 equivalente;
  - round-trip 38/38.
- **Defeito real encontrado e corrigido no pack:** peso de série sem máximo dava 500 (V1/U15).
- **Em aberto:**
  - aplicação pelo Leandro (`QG6F2-IMPL-001`, `QG6F2-CI-001`);
  - gates manuais (UI, isolamento, vídeo R2, peso);
  - `QG6F2-AUDIT-001` (`source-map-js`, só em desenvolvimento), numa tarefa separada.
