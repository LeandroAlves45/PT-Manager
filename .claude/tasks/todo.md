# TODO: sessão de 2026-10-10 · Sprint 6F-2 (Treino do portal), fecho do frontend

Plano aprovado em `~/.claude/plans/mighty-sparking-bumblebee.md`. Aplicação do Leandro em
`f0c827b` (backend, CI #29 verde) e `41c02a9` (frontend, CI #30 vermelho). Backend não se toca.

## 1. Diff programático
- [x] SHA-256 dos 38 caminhos contra o `manifest.json`: 21 iguais, 17 divergentes
- [x] Diff sem comentários nem formatação: 7 ficheiros com diferença real
- [x] Classificação: D1 e D2 (defeitos), copy do Leandro (fica), reticências, chave de cache

## 2. Estado recebido
- [ ] Lint, typecheck, Vitest, build e Prettier como chegaram

## 3. Correções
- [ ] D1: `plannedSetLabel` perde as casas decimais do peso e do RPE
- [ ] D2: `formatDecimal` com casa decimal fixa parte as repetições
- [ ] D3: códigos de validação dentro de `errors[]` nunca traduzidos
- [ ] Achados da revisão

## 4. Revisão
- [ ] `code-review-leandro` nos ficheiros de produção da fase
- [ ] `security-reviewer` (sonnet) no diff do frontend

## 5. Testes
- [ ] Testes do pack alinhados com a copy final
- [ ] Lacunas de caminhos reais cobertas
- [ ] Mutações FM1–FM21 repetidas e novas

## 6. Gates e documentação
- [ ] Lint, typecheck, Prettier, Vitest e build verdes
- [ ] Doc 08, relatório 09, blueprints 04–07 e manifest realinhados
- [ ] `backlogs/QualityGates.md`, `10_fecho_sprint_6F.md`, `00` da 6F
- [ ] Memória (`obsidian-ptmanager`), `lessons.md`, `GOTCHAS_FRONTEND.md`

## 7. Commit e CI
- [ ] Commit de fecho, push e CI verde
- [ ] `QG6F2-CI-001` marcado; checklist final

## Review

(a preencher no fecho)
