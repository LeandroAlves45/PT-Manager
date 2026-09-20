# Sprint 6B — blueprints de código real com validação (2026-09-18)

Plano: `C:\Users\Leandro Alves\.claude\plans\c-users-leandro-alves-desktop-projeto-p-moonlit-pony.md`.
Âmbito: documentação + materialização descartável em `C:\ptm6b` (branch `plan/sprint-6b`,
nunca merge). `backend/` real intocado. Decisões D1–D15 no plano.

## Execução

- [x] 0a. Bootstrap (ACTIVE, pack, roadmap, relatório §8, PNG) e exploração do backend
- [x] 0b. Decisões D1–D15 com o utilizador
- [x] 0c. Pattern `blueprints_codigo_real_por_ficheiro.md`: modo excerto (> 200 linhas)
- [x] 1. Worktree `C:\ptm6b` + baseline (build Debug/Release, contagem da suite)
- [x] 2. Domain: `TrainingPlanSchedule`
- [x] 3. Application: dashboard, resumo, portal (hoje, próximo check-in, home), filtros, moderação, overview, DI
- [x] 4. Infrastructure: queries, helper de dia UTC, DI
- [x] 5. Api: contratos e controllers
- [x] 6. Testes: Domain, Application, Integration (PostgreSQL), Functional, Architecture
- [x] 7. Budgets de queries (0 vs 120 registos) e EXPLAIN > 100 registos → decisão D4
- [x] 8. Snapshot OpenAPI 162 → 170 (+8, −0, 3 linhas de filtros alteradas)
- [x] 9. Suite integral Release verde + mutações dirigidas mortas
- [x] 10. Extração por script para docs 01–09 (completo ≤ 200 linhas / excerto > 200) + validador
- [x] 11. Docs 00, 10, 11, 12
- [x] 12. QualityGates.md, plan_sprint_6, README sprint-6, roadmap §6B, ACTIVE, MEMORY, nota de sessão
- [x] 13. Worktree removido; branch `plan/sprint-6b` local sem merge; `backend/` da main não foi
  tocado por esta sessão (os ficheiros novos em `backend/src` são da implementação do utilizador)

## Quality Gates (QG6B-*)

- [x] QG6B-DOC-001 Desenho e decisões D1–D15 fechadas
- [x] QG6B-DOC-002 Pack validado por script contra a materialização
- [x] QG6B-DOMAIN-001 Calendário cíclico
- [x] QG6B-APP-001 Dashboard e resumo do cliente
- [x] QG6B-APP-002 Portal: treino de hoje, próximo check-in, home
- [x] QG6B-APP-003 Filtros, fila de moderação, visão geral admin
- [x] QG6B-PERF-001 Budgets fixos (0 vs 120 registos), EXPLAIN, decisão D4
- [x] QG6B-TENANT-001 Isolamento entre dois trainers e entre clientes
- [x] QG6B-CONTRACT-001 Portal sem campos internos; fila sem email
- [x] QG6B-OPENAPI-001 Snapshot +8, −0
- [x] QG6B-TEST-001 Suite integral verde + mutações
- [ ] QG6B-IMPL-001 Aplicação manual no backend real (utilizador)

## Review

- Materialização em `C:\ptm6b` (branch `plan/sprint-6b`, sem merge): build 0/0, suite integral
  Release **2849 aprovados, 0 falhas, 1 skip** (+89), 6 mutações mortas, 105 blocos documentais
  sem divergências, snapshot 162 → 170.
- Migration `AddSprint6BReadIndexes` (3 índices parciais) justificada por EXPLAIN: 13,4 → 0,15 ms
  (fila de moderação) e 18,2 → 0,17 ms (check-ins por rever).
- Pack `docs/backend-files/sprint_6/sprint_6B/` (00–12). Falta `QG6B-IMPL-001` (aplicação real
  pelo utilizador). Worktree removido; branch mantida local para inspeção.
