# Stripe API Endpoints for PT Manager

PT Manager usa **Checkout Session + Subscriptions**, não Payment Intents avulsos. Core endpoints usados no fluxo de billing SaaS.

## Test Credentials

- Publishable Key: pk_test_51234567890abcdef
- Secret Key: sk_test_51234567890abcdef (NEVER commit)
- Webhook Secret: whsec_1234567890abcdef

## Create Checkout Session

POST https://api.stripe.com/v1/checkout/sessions

Request:
```json
{
    "mode": "subscription",
    "customer": "cus_1234567890",
    "line_items": [
        {
            "price": "price_1234567890",
            "quantity": 1
        }
    ],
    "success_url": "https://app.ptmanager.com/billing/success?session_id={CHECKOUT_SESSION_ID}",
    "cancel_url": "https://app.ptmanager.com/billing/cancel",
    "metadata": {
        "trainer_id": "uuid",
        "operation_id": "uuid"
    }
}
```

Response:
```json
{
    "id": "cs_1234567890",
    "url": "https://checkout.stripe.com/c/pay/cs_1234567890",
    "customer": "cus_1234567890",
    "mode": "subscription",
    "status": "open"
}
```

Corresponde a `CreateCheckoutHandler.HandleAsync` (`Application/Features/Billing/CreateCheckout/CreateCheckoutHandler.cs`), que devolve só `checkout.Url` (`Result<Uri>`) ao chamador.

## Retrieve Checkout Session

GET https://api.stripe.com/v1/checkout/sessions/{id}

Response: inclui `subscription` (ID da subscrição criada) quando `status = "complete"`.

## Retrieve Subscription

GET https://api.stripe.com/v1/subscriptions/{id}

Response:
```json
{
    "id": "sub_1234567890",
    "customer": "cus_1234567890",
    "status": "active",
    "current_period_end": 1735689600,
    "items": {
        "data": [
            { "price": { "id": "price_1234567890" } }
        ]
    }
}
```

`status` é o valor consumido por `SubscriptionStatusMapper.Map` (`Application/Features/Billing/Webhooks/SubscriptionStatusMapper.cs`).

## Cancel Subscription

POST https://api.stripe.com/v1/subscriptions/{id}

Request:
```json
{
    "cancel_at_period_end": true
}
```

Não existe operação de "refund" no core hoje — cancelamento é gerido pelo ciclo de vida da subscrição (`SubscriptionUpdated`/`SubscriptionDeleted`), não por reembolso de charge.

## List Invoices

GET https://api.stripe.com/v1/invoices?customer={customer_id}

Usado para reconciliar `InvoicePaymentSucceeded`/`InvoicePaymentFailed`.

## Test Cards

Visa: 4242 4242 4242 4242
Mastercard: 5555 5555 5555 4444
Failed: 4000 0000 0000 0002
3D Secure: 4000 0025 0000 3155

Any future expiry date, any CVC — usados só no Checkout hospedado da Stripe, nunca em formulário próprio.

## Headers

All requests require:
- Authorization: Bearer {secret_key}
- Content-Type: application/x-www-form-urlencoded (or application/json)
- Stripe-Version: 2020-08-27 (or later)
- Idempotency-Key: {unique_key}
