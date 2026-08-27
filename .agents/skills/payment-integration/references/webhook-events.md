# Stripe Webhook Events for PT Manager

PT Manager processa os 6 eventos mapeados em `PaymentEventKind`
(`Application/Features/Billing/Webhooks/PaymentEventKind.cs`). A verificação de assinatura e a
normalização do JSON raw da Stripe para `NormalizedPaymentEvent` são responsabilidade da
Infrastructure (a implementar); `ProcessPaymentWebhookHandler` já recebe o evento autenticado.

## 1. checkout.session.completed → CheckoutCompleted

Fired quando o Checkout Session é concluído com sucesso.

Payload:
```json
{
    "id": "evt_1234567890",
    "type": "checkout.session.completed",
    "data": {
        "object": {
            "id": "cs_1234567890",
            "customer": "cus_1234567890",
            "subscription": "sub_1234567890",
            "status": "complete",
            "metadata": {
                "trainer_id": "uuid"
            }
        }
    }
}
```

PT Manager Action:
1. Normalizar para `NormalizedPaymentEvent` com `Kind = CheckoutCompleted`
2. `ProcessPaymentWebhookHandler` obtém snapshot da subscrição via `ISubscriptionReconciliationGateway`
3. `IPaymentEventStore.CommitAsync` liga o customer/subscription à trainer subscription
4. Log de auditoria

## 2. customer.subscription.updated → SubscriptionUpdated

Fired quando a subscrição muda de estado (trial → active, active → past_due, etc).

Payload:
```json
{
    "id": "evt_1234567890",
    "type": "customer.subscription.updated",
    "data": {
        "object": {
            "id": "sub_1234567890",
            "customer": "cus_1234567890",
            "status": "past_due"
        }
    }
}
```

PT Manager Action:
1. `SubscriptionStatusMapper.Map("past_due")` → `SubscriptionStatus.Suspended`
2. `IPaymentEventStore.CommitAsync` atualiza `TrainerSubscription`
3. Log de auditoria

## 3. customer.subscription.deleted → SubscriptionDeleted

Fired quando a subscrição é cancelada definitivamente.

PT Manager Action:
1. `SubscriptionStatusMapper.Map("canceled")` → `SubscriptionStatus.Cancelled`
2. Revogar acesso do trainer ao fim do período pago
3. Log de auditoria

## 4. invoice.payment_succeeded → InvoicePaymentSucceeded

Fired quando uma fatura da subscrição é paga com sucesso (renovação mensal/anual).

Payload:
```json
{
    "id": "evt_1234567890",
    "type": "invoice.payment_succeeded",
    "data": {
        "object": {
            "id": "in_1234567890",
            "customer": "cus_1234567890",
            "subscription": "sub_1234567890",
            "amount_paid": 10000,
            "currency": "eur"
        }
    }
}
```

PT Manager Action:
1. Confirmar subscrição continua `Active`
2. Registar fatura para audit trail
3. Log de auditoria

## 5. invoice.payment_failed → InvoicePaymentFailed

Fired quando a cobrança de renovação falha.

PT Manager Action:
1. `SubscriptionStatusMapper` normalmente já reflete `past_due` via evento `SubscriptionUpdated` associado
2. Notificar trainer (email com link para atualizar método de pagamento)
3. Log de auditoria, alertar se falhas repetidas

## 6. customer.subscription.trial_will_end → TrialWillEnd

Fired ~3 dias antes do fim do trial.

PT Manager Action:
1. Notificar trainer que o trial está a terminar
2. Sem alteração de estado — é só aviso antecipado
3. Log de auditoria

## Eventos que NÃO existem no core hoje

`charge.succeeded`, `charge.failed`, `charge.refunded`, `payment_intent.*` — não fazem parte do
modelo de subscrição SaaS do PT Manager. Não implementar handlers para eles a menos que o produto
introduza pagamentos avulsos fora da subscrição.

## Webhook Processing Rules

- ALWAYS verify X-Stripe-Signature (no exceptions)
- ALWAYS check Event ID to prevent duplicates (via `IPaymentEventStore`)
- ALWAYS use idempotent processing (same event 2x = processed 1x)
- ALWAYS log without sensitive data
- ALWAYS retry failed webhook processing (exponential backoff)
- NEVER store card data
- NEVER throw exceptions (return 200 to Stripe, handle async)
