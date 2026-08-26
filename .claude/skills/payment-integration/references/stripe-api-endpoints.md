# Stripe API Endpoints for PT Manager

Core endpoints used in Payment Intent workflow.

## Test Credentials

- Publishable Key: pk_test_51234567890abcdef
- Secret Key: sk_test_51234567890abcdef (NEVER commit)
- Webhook Secret: whsec_1234567890abcdef

## Payment Intents Endpoint

POST https://api.stripe.com/v1/payment_intents

Request:
```json
{
    "amount": 10000,
    "currency": "eur",
    "payment_method_types": ["card"],
    "metadata": {
        "trainer_id": "uuid",
        "client_id": "uuid",
        "idempotency_key": "ptmanager_trainer_client_amount_op"
    }
}
```

Response:
```json
{
    "id": "pi_1234567890",
    "client_secret": "pi_1234567890_secret_abcdef",
    "status": "requires_payment_method",
    "amount": 10000,
    "currency": "eur"
}
```

## Retrieve Payment Intent

GET https://api.stripe.com/v1/payment_intents/{id}

Response: Same structure as above

## Confirm Payment Intent

POST https://api.stripe.com/v1/payment_intents/{id}/confirm

Request:
```json
{
    "payment_method": "pm_1234567890"
}
```

Response: Updated payment intent with status "succeeded" or "requires_action"

## Create Refund

POST https://api.stripe.com/v1/refunds

Request:
```json
{
    "payment_intent": "pi_1234567890",
    "amount": 5000,
    "reason": "requested_by_customer"
}
```

Response:
```json
{
    "id": "re_1234567890",
    "payment_intent": "pi_1234567890",
    "amount": 5000,
    "status": "succeeded"
}
```

## Retrieve Charges

GET https://api.stripe.com/v1/charges?payment_intent={id}

Response: List of charges for payment intent

## Test Cards

Visa: 4242 4242 4242 4242
Mastercard: 5555 5555 5555 4444
Failed: 4000 0000 0000 0002
3D Secure: 4000 0025 0000 3155

Any future expiry date, any CVC

## Headers

All requests require:
- Authorization: Bearer {secret_key}
- Content-Type: application/x-www-form-urlencoded (or application/json)
- Stripe-Version: 2020-08-27 (or later)
- Idempotency-Key: {unique_key}
