# Idempotency Strategy for Billing (Checkout + Subscriptions)

PT Manager deve prevenir duplo checkout se o trainer clicar submit 2x, e processar cada evento
de webhook Stripe apenas uma vez, mesmo que a Stripe reenvie o mesmo evento.

## Idempotency Key no Checkout

`CreateCheckoutHandler` já constrói a chave a partir de identidades reais do domínio, não de um
formato à parte:

```csharp
$"checkout:{actor.Value.TrainerId:N}:{command.OperationId:N}"
```

- `TrainerId` — quem está a subscrever
- `OperationId` — identifica esta tentativa específica de checkout (gerado pelo chamador, permite
  retry seguro do mesmo pedido sem criar duas Checkout Sessions)

Ver `Application/Features/Billing/CreateCheckout/CreateCheckoutHandler.cs`.

## Idempotência de Webhooks

Não se usa uma "Idempotency Key" gerada pela aplicação para deduplicar webhooks — usa-se o
**Stripe Event ID** (`evt_xxx`), que é único por natureza. `IPaymentEventStore.CommitAsync`
(`Application/Features/Billing/Abstractions/IPaymentEventStore.cs`) recebe o `NormalizedPaymentEvent`
já com o Event ID e devolve um `CommitPaymentEventStoreStatus`:

- `Processed` — primeira vez, processado agora
- `AlreadyProcessed` — Event ID já visto, idempotente (sem efeito colateral)
- `SubscriptionNotFound` / `ExternalIdentityConflict` / `ReconciliationRequired` /
  `ConcurrencyConflict` — falhas que `ProcessPaymentWebhookHandler` mapeia para `BillingErrors`

```csharp
// ProcessPaymentWebhookHandler.HandleAsync (real, já implementado)
var committed = await _store.CommitAsync(paymentEvent, snapshot, _clock.UtcNow, cancellationToken);

return committed.Kind switch
{
    CommitPaymentEventStoreStatus.Processed or
    CommitPaymentEventStoreStatus.AlreadyProcessed => Result.Success(),
    CommitPaymentEventStoreStatus.SubscriptionNotFound =>
        Result.Failure(BillingErrors.SubscriptionNotFound),
    // ...
};
```

## Stripe Idempotency Header (chamadas de saída)

Toda chamada de escrita ao SDK Stripe (criar Checkout Session, etc.) deve enviar
`Idempotency-Key` no `RequestOptions`, usando a mesma chave de operação do domínio:

```csharp
var requestOptions = new RequestOptions
{
    IdempotencyKey = $"checkout:{trainerId:N}:{operationId:N}",
    ApiKey = stripeSk
};
```

Stripe devolve o resultado anterior se a mesma chave for reenviada dentro de 24 horas — protege
contra duplo-clique e retry de rede.

## Cleanup

- Stripe idempotency keys: válidas 24 horas (gerido pela Stripe, não pela app)
- Eventos processados (`IPaymentEventStore`): reter conforme política de audit trail (ver
  `payment-security` — 1 ano para compliance), não 90 dias como num modelo de pagamento avulso

## Benefits

- Previne duas Checkout Sessions se o browser reenviar o pedido
- Previne processar o mesmo webhook duas vezes se a Stripe reentregar
- Suporta retry seguro sem efeitos colaterais duplicados
