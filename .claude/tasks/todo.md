# TODO: sessão de 2026-10-11 · Sprint 6F-3 (Nutrição e suplementos do portal), fecho

Plano aprovado em `~/.claude/plans/cryptic-wondering-hellman.md`. Só frontend: `backend/` não é
tocado. Um processo pesado de cada vez, 2 workers.

## 1. Comparação com o pack
- [x] SHA-256 dos 13 caminhos contra o `manifest.json` (5 iguais, 8 divergentes)
- [x] Diff bloco a bloco dos 8 divergentes: só comentários e uma frase de copy
- [x] Patch de `portal-fixtures.ts`: só o comentário de abertura mudou de forma

## 2. Baseline no checkout real
- [x] `npm run lint` e `npm run typecheck`
- [x] Vitest dos 3 ficheiros da fase (37/37)

## 3. Revisão e correções
- [x] `code-review-leandro` aos 8 ficheiros de produção
- [x] A1: JSDoc "falha de marcar" (`SupplementIntakeRow`)
- [x] A2: ponto final no JSDoc de `TargetsTable`
- [x] A3: `intakeFailureMessage` lê só `failure.code` (ramo `errors[]` morto nestes endpoints)
- [x] A4: `isError` com dados em cache igual ao padrão da 6F-2; registado, sem alteração

## 4. Mutações no código real
- [x] M01–M34 do pack repetidas
- [x] M35–M50 novas
- [x] Sobreviventes tratadas (teste novo ou equivalente justificada)

## 5. Gates finais
- [x] lint, typecheck, Vitest completo, build, Prettier, `npm audit`

## 6. Documentação
- [x] Blueprints 02–05 e manifest realinhados (round-trip 13/13)
- [x] `06_6F3_quality_gates.md` e `backlogs/QualityGates.md`
- [x] `07_relatorio_fecho_fase_6F3.md`, `01` (estado), `10_fecho_sprint_6F.md`
- [x] Memória: ACTIVE, MEMORY, NEST, nota de sessão, lessons, GOTCHAS_FRONTEND

## 7. Fecho
- [x] Commit e push
- [x] CI do GitHub Actions verde no commit de fecho

## Review

- A aplicação do Leandro (`8ae93a4`) chegou verde e fiel ao pack: 8 de 13 ficheiros divergiam do
  manifest só em comentários e numa frase de copy.
- Três correções pequenas em `858e7f4`: dois typos de JSDoc e o ramo morto
  `fieldErrors[0]?.code` em `intakeFailureMessage`.
- 50 mutações no código real (34 do pack e 16 novas), todas mortas pelos 37 testes existentes:
  não se acrescentaram testes.
- Gates: lint, typecheck, Vitest 419/419, build e Prettier verdes; round-trip 13/13.
- CI: #33 verde em `8ae93a4`; CI #34 verde em `858e7f4`.
- `design-is` dos dois ecrãs: 20/30, REFINE; artefactos em `frontend/DESIGN-IS-2026-10-11/`.
- Ficam com o Leandro: `QG6F3-UI-001` e `QG6F3-ISOLAMENTO-UI-001` (login no browser).
- Por decidir no fecho do sprint: A4 (`isError` antes dos dados nos três ecrãs do portal).

**Fase 6F-3: Finalizado.**
