# Sessão: auditoria de encerramento do Sprint 1 — 2026-07-28

Foco: comparar `docs/backend-files/sprint_1`, Domain, testes, arquitetura e
schema para confirmar o encerramento do Sprint 1.

## Resultado

Sprint 1 finalizado: 27 entidades, 5 Value Objects, `DomainException`,
`ITenantContext`, `IClock` e 24 ficheiros de testes com 68 métodos, executados
como 106 casos xUnit. O utilizador confirmou build Release, format e suite
completa verdes.

Achados corrigidos:

1. Teste de idempotência de `RefreshToken.Revoke` usa a mesma instância.
2. Entidades mutáveis com soft delete aplicam fail closed.
3. `Food` e `PackType` guardam os nomes normalizados que validam.
4. `ExerciseSet` rejeita carga e descansos negativos.
5. Docs, schema e memórias estão sincronizados.

Mantêm-se bloqueantes antes da `InitialCreate`: integridade cross-tenant,
owner/renovação de leases e recuperação da outbox depois de crash.

## Decisão

Marcar o Sprint 1 como Finalizado em 28 de julho de 2026. Os bloqueios
cross-tenant, leases e outbox permanecem obrigatórios antes da `InitialCreate`
no Sprint 2.
