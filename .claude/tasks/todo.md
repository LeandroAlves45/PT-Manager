# TODO: sessão de 2026-10-10 · Sprint 6F-2 (Treino do portal), fecho do frontend

Plano aprovado em `~/.claude/plans/mighty-sparking-bumblebee.md`. Aplicação do Leandro em
`f0c827b` (backend, CI #29 verde) e `41c02a9` (frontend, CI #30 vermelho). Backend não se toca.

## 1. Diff programático
- [x] SHA-256 dos 38 caminhos contra o `manifest.json`: 21 iguais, 17 divergentes
- [x] Diff sem comentários nem formatação: 7 ficheiros com diferença real
- [x] Classificação: D1 e D2 (defeitos), copy do Leandro (fica), reticências, chave de cache

## 2. Estado recebido
- [x] Lint e typecheck verdes; 12 testes vermelhos nos 3 ficheiros do portal

## 3. Correções
- [x] D1: `plannedSetLabel` perdia as casas decimais do peso e do RPE
- [x] D2: `formatDecimal` com casa decimal fixa partia as repetições
- [x] D3: códigos de validação dentro de `errors[]` nunca traduzidos (`workoutFailureMessage`)
- [x] D4: repetir o vídeo depois de uma falha passageira
- [x] D5: erro antigo no diálogo de concluir
- [x] D6: `aria-valuenow` na barra de progresso
- [x] D7: mensagem genérica, reticências, JSDoc

## 4. Revisão
- [x] `code-review-leandro` nos ficheiros de produção da fase
- [x] `security-reviewer` (sonnet) no diff do frontend: sem vulnerabilidades exploráveis

## 5. Testes
- [x] Testes do pack alinhados com a copy final
- [x] 10 testes novos de caminhos reais
- [x] Mutações FM1–FM21 repetidas e FM22–FM33 novas: 33/33 mortas

## 6. Gates e documentação
- [x] Lint, typecheck, Prettier, Vitest 382/382 e build (301,15 kB gzip) verdes
- [x] Doc 08, relatório 09, blueprints 04–07 e manifest realinhados
- [x] `backlogs/QualityGates.md`, `10_fecho_sprint_6F.md`, `00` da 6F
- [x] Memória (`obsidian-ptmanager`), `lessons.md`, `GOTCHAS_FRONTEND.md`

## 7. Commit e CI
- [x] Commit de fecho `5310809` e push
- [x] CI #31 verde em `5310809` (4/4 jobs); `QG6F2-CI-001` marcado

## Review

**Finalizado.**

- **O que mudou:** 8 ficheiros de produção do frontend e 3 de teste. Backend sem alterações.
- **Defeitos:** D1 e D2 vieram de duas trocas de uma palavra na aplicação manual; D3 era do
  próprio pack; D4–D7 são melhorias pequenas encontradas na revisão.
- **Copy do Leandro:** mantida, exceto a mensagem genérica "guardar a série", que também
  aparecia ao desmarcar e ao concluir.
- **Evidência:** Vitest 382/382, 33/33 mutações, build 301,15 kB gzip, CI no commit de fecho.
- **Incidente:** o Prettier corrido sobre cópias no `%TEMP%` bloqueou a máquina três vezes;
  regra nova em `GOTCHAS_FRONTEND.md`.
- **Em aberto (Leandro):** gates manuais `QG6F2-UI-001`, `QG6F2-ISOLAMENTO-UI-001`,
  `QG6F2-VIDEO-001`, `QG6F2-PESO-001`; `QG6F2-AUDIT-001`; CSP da SPA; blueprints da 6F-3.
