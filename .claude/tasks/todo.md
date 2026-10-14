# TODO: sessão de 2026-10-13 · Fecho da 6F-4 e do Sprint 6F

Plano aprovado em `~/.claude/plans/virtual-napping-globe.md`.
Base: `5ddacd8` (CI #38 verde). Aplicação do pack em `16b572f` (backend) e `5ddacd8` (frontend).

## 1. Análise (feita em modo plano, sem editar)
- [x] Diff programático contra o `manifest.json`: 40 iguais, 13 divergentes
- [x] Linha de base: backend 2986 + 1 skip, Vitest 544, lint, typecheck, build
- [x] Skill `code-review-leandro` sobre a 6F-4
- [x] Skill `performance-reviewer` (check-ins, menu do topo, refetch do portal)
- [x] Três agentes (sonnet) sobre o 6F inteiro: comportamento, segurança, cobertura
- [x] Achados verificados no código; decisões do Leandro recolhidas

## 2. Backend
- [ ] D1: `$` em falta em `InitialAssessment.ValidateParameters` + teste de domínio
- [ ] S1: escala de duas casas no peso e na massa gorda (sonda do 500 primeiro)
- [ ] S2: `client.IsActive` nas duas leituras de suplementos do portal + teste
- [ ] S3: `\u0000` em texto (sonda primeiro; corrigir só se der 500)
- [ ] Lacunas: `UpdateInitialAssessment` com peso 0; limite literal 1000
- [ ] `GOTCHAS_BACKEND.md`
- [ ] Suite completa verde

## 3. Frontend
- [ ] D2: duplo toque em "Seguinte" envia o check-in (teste primeiro)
- [ ] D3: ramo morto `portal_profile_not_available` em Treino e Plano
- [ ] `phone` de `toProfileValues` como no blueprint
- [ ] Mutações dirigidas e testes só para as sobreviventes
- [ ] `GOTCHAS_FRONTEND.md`
- [ ] Vitest, lint, typecheck e build verdes

## 4. Blueprints e gates
- [ ] Realinhar docs 02–07 e `manifest.json` com o código final (round-trip 53/53)
- [ ] `sprint_6F4/08_6F4_quality_gates.md`
- [ ] `backlogs/QualityGates.md`

## 5. Relatórios
- [ ] `sprint_6F4/09_relatorio_fecho_fase_6F4.md`
- [ ] `10_fecho_sprint_6F.md`
- [ ] `00_plano_fase_6F_completa.md`

## 6. Memória
- [ ] Nota em `.claude/memory/Sessions/`
- [ ] `MEMORY.md`, `ACTIVE.md`, `lessons.md`
- [ ] Memória automática do projeto

## 7. Commit e CI
- [ ] Commits em `main` e `git push`
- [ ] Run de CI verde
- [ ] Checklist final e secção Review
