# Stripe Webhook Events for PT Manager

PT Manager handles 5 critical webhook events.

## 1. charge.succeeded

Fired when charge completes successfully.

Payload:
```json
{
    "id": "evt_1234567890",
    "type": "charge.succeeded",
    "data": {
        "object": {
            "id": "ch_1234567890",
            "amount": 10000,
            "currency": "eur",
            "payment_intent": "pi_1234567890",
            "status": "succeeded",
            "metadata": {
                "trainer_id": "uuid"
            }
        }
    }
}
```

PT Manager Action:
1. Retrieve payment_intent from webhook
2. Mark payment as "completed" in database
3. Create invoice for customer
4. Send confirmation email
5. Log audit trail

## 2. charge.failed

Fired when charge fails.

Payload: Same structure, status = "failed", failure_code = "card_declined"

PT Manager Action:
1. Mark payment as "failed"
2. Store failure reason (card_declined, insufficient_funds, etc)
3. Notify user (email with retry link)
4. Log audit trail
5. Alert if repeated failures from same user

## 3. charge.refunded

Fired when charge is refunded.

Payload:
```json
{
    "id": "evt_1234567890",
    "type": "charge.refunded",
    "data": {
        "object": {
            "id": "ch_1234567890",
            "refunds": {
                "data": [
                    {
                        "id": "re_1234567890",
                        "amount": 5000,
                        "reason": "requested_by_customer"
                    }
                ]
            }
        }
    }
}
```

PT Manager Action:
1. Retrieve refund amount and reason
2. Update payment status to "refunded" or "partially_refunded"
3. Create credit memo
4. Send refund confirmation email
5. Log audit trail

## 4. payment_intent.requires_action

Fired when 3D Secure or other additional authentication required.

Payload:
```json
{
    "id": "evt_1234567890",
    "type": "payment_intent.requires_action",
    "data": {
        "object": {
            "id": "pi_1234567890",
            "status": "requires_action",
            "next_action": {
                "type": "use_stripe_sdk",
                "use_stripe_sdk": {}
            }
        }
    }
}
```

PT Manager Action:
1. Notify user that 3D Secure authentication required
2. Send link to complete authentication
3. Store payment intent for later confirmation
4. Log audit trail

## 5. payment_intent.succeeded

Fired when payment intent succeeds end-to-end.

Payload:
```json
{
    "id": "evt_1234567890",
    "type": "payment_intent.succeeded",
    "data": {
        "object": {
            "id": "pi_1234567890",
            "status": "succeeded",
            "charges": {
                "data": [
                    {
                        "id": "ch_1234567890",
                        "amount": 10000
                    }
                ]
            }
        }
    }
}
```

PT Manager Action:
1. Mark payment intent as "succeeded"
2. Create invoice
3. Grant access to service
4. Send success email
5. Log audit trail

## Webhook Processing Rules

- ALWAYS verify X-Stripe-Signature (no exceptions)
- ALWAYS check Event ID to prevent duplicates
- ALWAYS use idempotent processing (same event 2x = processed 1x)
- ALWAYS log without sensitive data
- ALWAYS retry failed webhook processing (exponential backoff)
- NEVER store card data
- NEVER throw exceptions (return 200 to Stripe, handle async)
