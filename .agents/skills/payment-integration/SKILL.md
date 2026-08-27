---
name: payment-integration
description: Expert em integração Stripe no ASP.NET Core. Setup SDK, Checkout Sessions, subscription lifecycle (SaaS billing), webhooks, idempotência e retry logic.
color: green
emoji: 💳
vibe: Payment processing é crítico. Implementa com idempotência, retry automático e zero tolerância a ambiguidade.
---

# Payment Integration Specialist — Stripe

Expert em integração Stripe no backend C# .NET 10. Responsável por setup SDK, Checkout Sessions, subscription lifecycle (SaaS billing), webhook management, idempotência e tratamento de erros.

PT Manager é billing por **subscrição recorrente** (trainer paga plano SaaS), não pagamento avulso por card. O modelo é Stripe Checkout Session + Subscriptions, nunca Payment Intent/charge direto. A Application layer já existe (`Application/Features/Billing/`); a Infrastructure com o SDK Stripe real, endpoint de webhook e verificação de assinatura ainda está por implementar — esta skill descreve o alvo a seguir quando essa camada for escrita.

## 🎯 Core Mission

### Stripe SDK Setup & Configuration

- Adicionar Stripe.net NuGet (versão stable)
- Configurar chaves (Secret, Publishable, Webhook)
- Variáveis de ambiente para test vs live mode
- Timeout e retry configuration
- Idempotency key generation strategy

### Checkout Session Workflow

- `CreateCheckoutHandler` (já existe em `Application/Features/Billing/CreateCheckout/`) orquestra via `ICheckoutGateway.CreateCheckoutAsync`, sem conhecer o SDK Stripe diretamente
- Backend cria a Checkout Session e devolve apenas a `Url` de redirect (`Result<Uri>`)
- Cliente é redirecionado para o Checkout hospedado pela Stripe — nunca há formulário de card próprio nem `paymentMethodId` a passar pelo backend
- Stripe confirma via webhook (`checkout.session.completed`), nunca por chamada direta do frontend
- Nunca armazenar card data raw

### Subscription Lifecycle

- Estado da subscrição vive em `TrainerSubscription` (Domain) e é atualizado via `SubscriptionStatusMapper`
- Mapeamento de status Stripe → interno: `trialing`/`active` → `Active`; `past_due`/`unpaid`/`paused` → `Suspended`; `canceled` → `Cancelled`; `incomplete`/`incomplete_expired` → `Inactive`
- Sem conceito de refund/charge parcial — cancelamento e downgrade são geridos pelo ciclo de vida da subscrição na Stripe
- Validar amount em cents (não decimais) sempre que aplicável (ex.: valores de plano)

### Webhook Management

- Receber eventos de Stripe (raw JSON), verificar assinatura e normalizar antes de chegar à Application layer
- Eventos suportados hoje pelo core (`PaymentEventKind`): `CheckoutCompleted`, `SubscriptionUpdated`, `SubscriptionDeleted`, `InvoicePaymentSucceeded`, `InvoicePaymentFailed`, `TrialWillEnd`
- `ProcessPaymentWebhookHandler` já assume o evento autenticado e normalizado (`NormalizedPaymentEvent`) — a verificação de assinatura + parsing do JSON raw da Stripe é responsabilidade da Infrastructure, ainda por implementar
- Retry automático para webhook failures
- Idempotência (mesmo webhook recebido 2x = processado 1x) via `IPaymentEventStore`

### Error Handling & Retry Logic

- Transient errors: retry com backoff exponencial
- Non-transient errors: falhar imediatamente
- Result<T> pattern (falhas esperadas = não exceptions)
- Logging sem dados sensíveis
- Stripe error codes mappings

## 🚨 Critical Rules

### Nunca Tocar Card Data Raw

- Checkout é hospedado pela Stripe (redirect completo) — card nunca passa por infraestrutura própria
- Backend NUNCA recebe número de card, CVV, ou magnetic stripe
- Backend só lida com identificadores Stripe (Customer ID, Subscription ID, Checkout Session ID), nunca dados de card

### Idempotência é Obrigatória

- Cada operação tem Idempotency Key única
- Stripe retorna resultado anterior se mesma chave enviada
- Evita duplicação se cliente clica 2x

### Amount é em Cents, não Decimais

- 100 EUR = 10000 cents
- Armazenar em cents no database (integer)
- Nunca usar decimals para cálculos (rounding errors)

### Webhook Signature Verification

- SEMPRE verificar X-Stripe-Signature header
- Nunca processar webhook sem signature check
- Stripe service down? Webhook pode estar fake

### Retry Strategy é Explícita

- 429 (rate limit) → retry com Retry-After
- 5xx (server error) → retry com backoff exponencial
- 4xx (validation error) → falhar imediatamente
- Max 3 retries, exponential backoff (100ms, 200ms, 400ms)

## Referências

Ver `.claude/skills/payment-integration/references/` para:
- stripe-api-endpoints.md
- webhook-events.md
- idempotency-strategy.md
- stripe-error-codes.md

Ver `.claude/skills/payment-integration/examples/` para código C#.
