# TODO — Sessão 2026-10-02 · Fecho 6E-4 frontend

Objetivo: rever docs 03–08 aplicados pelo Leandro (`88d3dfa`), cobrir lacunas reais, code review + performance, JSDoc, gates, CI, docs e fecho. Backend fechado: não tocar.

- [x] Diff programático blueprint ↔ código: 10 iguais, 13 com desvios pequenos (renomeação `mealsList`/`mealsDetail`, JSDoc, copy "com sucesso", typo "reconcializa", timeouts de teste).
- [x] Baseline: `npm ci`, lint, typecheck, format limpos; 234/234 testes (315 s, 71 % import+environment).
- [x] Revisão funcional vs doc 01 (A1, B3, B5–B8, conflito 409, orçamento HTTP).
- [x] `code-review-leandro` + `performance-reviewer` (agente sonnet): 0 HIGH.
- [x] Correções D1 (histórico a meio da edição), D2 (`isPending` eterno), D3 (reativação sem saída), cada uma com teste vermelho antes.
- [x] Lacunas: A1 no editor alimentar, B3, `calculation: null` no PUT (conflito ativo já coberto).
- [x] Mutações FM4–FM9 6/6 mortas.
- [x] JSDoc dos ficheiros novos + typo.
- [x] Gates verdes (236 testes), commit `e6c5c4e`, push, CI #22 verde (4 jobs).
- [x] Docs: 09, QualityGates, relatório 10, doc 01.
- [x] Memória (obsidian-ptmanager) e checklist de fecho.

## Review
Fase 6E-4 FINALIZADA. Pendentes só os gates manuais `QG6E4-ISOLAMENTO-UI-001` e `QG6E4-UI-001`.

# TODO — Sessão 2026-10-01 · Revisão do pack 6E-4, testes backend e contrato

Objetivo: rever o plano do codex contra o código real, implementar os testes backend do doc 08, aplicar melhorias, regenerar `schema.d.ts` e corrigir os blueprints frontend. Não tocar no frontend, exceto no contrato.

- [x] Agentes sonnet: análise do plano (01–09) e do backend aplicado pelo Leandro.
- [x] Testes do doc 08 aplicados, mais lacunas: histórico por série, isolamento das listas, `client_name` null na escrita, `*_input` no preview `percentage` e no detalhe.
- [x] M1 `TrainingPlanHistory` (fonte única), M2 `client_name` obrigatório e anulável, M3 tenant explícito na subquery.
- [x] Mutações backend MU1–MU6 6/6 mortas.
- [x] Backend: build Release com 0 avisos; 2911 testes, 1 skip, 0 falhas; `dotnet format` limpo.
- [x] Contrato: `npm run api:types` e `api:types:check` verdes, 14 linhas aditivas.
- [x] Blueprints frontend: A1, B3, B5–B8, testes de regressão, worktree descartável verde, mutações FM1–FM3 3/3.
- [x] Docs 01/02/08/09 e manifest (46 caminhos) atualizados; round-trip verificado.
- [ ] Leandro: aplicar o frontend (docs 03–08) ao checkout real e fechar os gates manuais (login, 1440/768/375, claro/escuro).
- [ ] CI real depois do commit.

# TODO — Sessão 2026-09-30 · Blueprints da fase 6E-4

Objetivo: materializar e validar na worktree descartável `sprint-6e4-blueprints` o código de planos de treino, séries, nutrição e suplementos. Extrair o pack documental para `docs/blueprints/frontend-files/sprint_6/sprint_6E/sprint_6E4/`. Não aplicar código de produção no repositório principal.

## Decisões confirmadas

- Histórico de treino bloqueia a estrutura e as datas logo ao abrir.
- Preview nutricional explícito e obrigatório após alterar o cálculo.
- Suplementos por refeição e atribuições ao cliente na mesma fatia.
- Atribuições na ficha do cliente e em página geral com filtro.
- Dados clínicos disponíveis são sugestões editáveis.

## Trabalho

- [x] Confirmar base Git limpa e criar worktree temporária.
- [x] Verificar contratos e identificar campos aditivos necessários.
- [x] Concluir materialização temporária e testes backend/frontend.
- [x] Gerar e comparar `schema.d.ts` a partir do OpenAPI.
- [x] Extrair blueprints completos e comparar blocos/excertos programaticamente.
- [x] Correr gates, mutações dirigidas e registar evidência.
- [x] Atualizar QualityGates e memória.
- [x] Verificar checkout principal e descartar worktree e branch.
