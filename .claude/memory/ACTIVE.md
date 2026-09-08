# Estado ativo: Sprint 5A FECHADA — próximo 5B

Atualizado: 2026-09-08
Próximo passo: aplicar a migration `20260908141012_AddQStashDispatchReceipts`
à base Docker local, gravar user-secrets QStash, e só depois planear a Fase 5B.
`QStash:Enabled` permanece `false`.

## Estado em uma linha

O Sprint 4 está finalizado. A Fase 5A está fechada no backend. Não activar
QStash até migration local, secrets e preflight SQL no ambiente persistente.

## Evidência

1. Fecho: `docs/backend-files/sprint_5/sprint_5A/15_revisao_implementacao_validacao.md`.
2. `dotnet test PTManager.sln --configuration Release`: 2148 aprovados, 1 ignorado.
3. Format verify e modelo EF sem pending changes.
4. Outbox sem handlers já não reclama `billing_notification` nem `trainer-logo.delete`.

## Bloqueios de rollout QStash

1. `QStash:Enabled` permanece `false`.
2. A migration local ainda não foi aplicada (decisão do utilizador).
3. Não existe job global na allowlist inicial.

## Ler nesta ordem

1. Este ficheiro.
2. `.claude/memory/Sessions/2026-09-08-sprint5a-fecho.md`.
3. `docs/backend-files/sprint_5/sprint_5A/15_revisao_implementacao_validacao.md`.
4. `.claude/project/sprints/` quando existir pack da Fase 5B.
5. `backlogs/QualityGates.md`, secção Sprint 5 Fase 5A.
