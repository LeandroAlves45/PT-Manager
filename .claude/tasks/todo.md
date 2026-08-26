# Lote 3G-C_D — fecho do gate Billing (2026-08-26)

## Plano

- [x] 1. Corrigir `PaymentEventStore`: execution strategy + `ExecuteInTransactionAsync`, mapeamento completo de `BillingEventApplyStatus`, payload snake_case, trainer inativo sem poison event, tenant estabelecido uma única vez antes do delegate.
- [x] 2. Corrigir `BillingCheckoutStore.LinkCustomerAsync`: execution strategy + verificação de sucesso por identidade persistente (customer associado ao trainer).
- [x] 3. Domain.UnitTests: cobrir ausência de mutação parcial em `ApplyProviderSnapshot` com subscription ID inválido.
- [x] 4. Application.UnitTests: `ProcessPaymentWebhookHandler` + `SubscriptionStatusMapper` cobertos.
- [x] 5. Infrastructure.IntegrationTests: `PaymentEventStoreTests` (9 cenários) — compilados, execução bloqueada pela migration pendente.
- [x] 6. Review Authentication: seis stores Identity corrigidos (transações fora da execution strategy) + teste de integração com contexts `EnableRetryOnFailure`; suites verdes.
- [x] 7. Build Release 0W/0E; Domain 381, Application 451, Architecture 36; `dotnet format` verify limpo.
- [x] 8. Ficheiro md final: `docs/backend-files/lote_3G/lote_3G-C_D/12_gate_3gd_fecho_pre_migration.md`.
- [x] 9. Memória atualizada (`MEMORY.md` item 11 + `Sessions/2026-08-26-lote-3g-c-d-fecho-pre-migration.md`).

## Review

- Um teste de Domain pré-existente foi corrigido (não o domínio): o cenário de
  snapshot obsoleto usava um customer diferente, o que dispara o conflito de
  identidade antes da verificação de staleness — comportamento correto e
  desejado do aggregate.
- Achado fora do enunciado mas dentro da review Auth: os seis stores Identity
  abriam transações fora da execution strategy; com `EnableRetryOnFailure`
  (produção) lançariam `InvalidOperationException`. Corrigidos com o padrão de
  `NotificationQueueStore` (recheck de commit ambíguo por identidade única).
- Nada de migrations tocado. Suite PostgreSQL fica bloqueada até o utilizador
  gerar a migration consolidada; os testes de integração novos já compilam.
