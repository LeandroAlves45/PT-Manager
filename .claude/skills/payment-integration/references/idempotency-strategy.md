# Idempotency Strategy for Payment Intents

PT Manager must prevent duplicate payments if client clicks submit 2x.

## Idempotency Key Format

```
ptmanager_{trainer_id}_{client_id}_{amount_cents}_{operation}
```

Example:
```
ptmanager_550e8400-e29b-41d4-a716-446655440000_uuid_10000_payment_intent_create
```

## Key Components

1. `ptmanager_` — Application prefix (prevents collision with other services)
2. `{trainer_id}` — Who owns the payment intent (UUID)
3. `{client_id}` — Who is paying (UUID)
4. `{amount_cents}` — Amount in cents (10000 = 100 EUR)
5. `{operation}` — What operation: `payment_intent_create`, `charge`, `refund`

## Implementation in C#

```csharp
public class IdempotencyKeyGenerator
{
    public static string GeneratePaymentIntentKey(
        Guid trainerId,
        Guid clientId,
        long amountCents)
    {
        return $"ptmanager_{trainerId}_{clientId}_{amountCents}_payment_intent_create";
    }

    public static string GenerateChargeKey(
        Guid trainerId,
        Guid clientId,
        long amountCents)
    {
        return $"ptmanager_{trainerId}_{clientId}_{amountCents}_charge";
    }

    public static string GenerateRefundKey(
        Guid trainerId,
        Guid clientId,
        long amountCents)
    {
        return $"ptmanager_{trainerId}_{clientId}_{amountCents}_refund";
    }
}
```

## Stripe Idempotency Header

Send with every payment request:

```csharp
var idempotencyKey = IdempotencyKeyGenerator.GeneratePaymentIntentKey(
    trainerId,
    clientId,
    amountCents);

var requestOptions = new RequestOptions
{
    IdempotencyKey = idempotencyKey,
    ApiKey = stripeSk
};

var paymentIntent = await StripeClient.CreatePaymentIntentAsync(
    options,
    requestOptions);
```

## Stripe Idempotency Behavior

1. First request with key X → Stripe processes, returns result
2. Second request with key X (within 24 hours) → Stripe returns cached result
3. Different key → Stripe processes as new request

This guarantees: **Same key = Same result, even if sent 10 times**

## Database Idempotency Log

PT Manager must also track processed operations:

```csharp
public class IdempotencyLog
{
    public int Id { get; set; }
    public Guid TrainerId { get; set; }
    public string IdempotencyKey { get; set; }
    public string Operation { get; set; }
    public string StripeEventId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

## Processing Logic

```csharp
public async Task<Result<Payment>> CreatePaymentAsync(
    Guid trainerId,
    Guid clientId,
    long amountCents,
    CancellationToken ct)
{
    var idempotencyKey = IdempotencyKeyGenerator.GeneratePaymentIntentKey(
        trainerId,
        clientId,
        amountCents);

    // Check if already processed
    var existing = await _idempotencyLog.GetAsync(idempotencyKey);
    if (existing != null)
    {
        _logger.LogInformation("Duplicate request, returning cached: {Key}", idempotencyKey);
        return Result.Success(await _paymentRepository.GetByIdAsync(existing.StripeEventId));
    }

    // Process new request
    var paymentIntent = await _stripeService.CreatePaymentIntentAsync(
        trainerId,
        clientId,
        amountCents,
        idempotencyKey);

    // Log for future duplicate detection
    await _idempotencyLog.AddAsync(new IdempotencyLog
    {
        TrainerId = trainerId,
        IdempotencyKey = idempotencyKey,
        Operation = "payment_intent_create",
        StripeEventId = paymentIntent.Id,
        ProcessedAt = DateTime.UtcNow
    });

    return Result.Success(/* ... */);
}
```

## Cleanup

- Idempotency keys valid for 24 hours (Stripe)
- Database logs: keep 90 days (compliance + support)
- After 90 days: delete old logs

```csharp
// Scheduled job (daily)
await _idempotencyLog.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-90));
```

## Benefits

- Prevents duplicate charges if browser refreshes
- Prevents double-debit if network retry
- Supports safe retry logic without side effects
- Stripe + PT Manager both prevent duplicates
