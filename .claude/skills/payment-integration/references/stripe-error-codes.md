# Stripe Error Codes & Handling

PT Manager must handle errors gracefully and inform users appropriately.

## Transient Errors (Retry with Backoff)

### 429: Too Many Requests (Rate Limit)

Handling:
```csharp
if (ex.StripeResponse?.StatusCode == 429)
{
    var retryAfter = ex.StripeResponse?.Headers?["Retry-After"];
    await Task.Delay(TimeSpan.FromSeconds(int.Parse(retryAfter ?? "2")));
    return await Retry(operation);
}
```

User Message: "Muitos pedidos. Por favor, tente novamente em alguns segundos."

### 5xx: Server Error

Handling: Retry with exponential backoff (100ms, 200ms, 400ms)

User Message: "Serviço indisponível. Tentando novamente..."

## Non-Transient Errors (Fail Immediately)

### card_declined: Card Declined

Cause: Issuing bank declined transaction
User Action: Try different card or contact bank
User Message: "Cartão recusado. Por favor, verifique os detalhes."

### expired_card: Card Expired

Cause: Card expiration date passed
User Action: Update card
User Message: "Cartão expirado. Por favor, use um cartão válido."

### incorrect_cvc: Incorrect CVC

Cause: Wrong CVV
User Action: Check CVV and retry
User Message: "Código CVV incorreto. Por favor, verifique e tente novamente."

### processing_error: Processing Error

Cause: Stripe backend issue (rare)
User Action: Retry later or contact support
User Message: "Erro ao processar. Por favor, tente novamente mais tarde."

### authentication_error: Authentication Failed

Cause: Invalid Stripe key, webhook secret, etc.
User Action: Check configuration
User Message: Log only (never show to user)

### invalid_request_error: Invalid Request

Cause: Malformed request (amount, currency, etc.)
User Action: Fix request parameters
User Message: Log only (engineering issue)

### card_error: Generic Card Error

Cause: Various card issues
User Action: Try different card
User Message: "Cartão inválido. Por favor, use outro."

## Error Response Structure

```json
{
    "error": {
        "code": "card_declined",
        "message": "Your card was declined.",
        "type": "card_error",
        "charge": "ch_1234567890",
        "decline_code": "generic_decline"
    }
}
```

## Handling Strategy

```csharp
public async Task<Result<PaymentIntent>> CreatePaymentAsync(...)
{
    try
    {
        var result = await _stripeClient.CreatePaymentIntentAsync(...);
        return Result.Success(result);
    }
    catch (StripeException ex)
    {
        _logger.LogError("Stripe error: {Code} - {Message}", ex.StripeError.Code, ex.StripeError.Message);

        return ex.StripeError.Code switch
        {
            "card_declined" => Result.Failure("Cartão recusado."),
            "expired_card" => Result.Failure("Cartão expirado."),
            "incorrect_cvc" => Result.Failure("CVV incorreto."),
            "processing_error" => Result.Failure("Erro ao processar."),
            "rate_limit_error" => Result.Failure("Muitos pedidos. Tente novamente."),
            _ => Result.Failure("Erro desconhecido ao processar pagamento.")
        };
    }
}
```

## Decline Codes (Detailed)

- generic_decline: Issuer declined for unknown reason
- insufficient_funds: Not enough money
- lost_card: Card reported lost
- stolen_card: Card reported stolen
- expired_card: Card expired
- incorrect_cvc: Wrong CVC
- authentication_required: 3D Secure required
- processing_error: Stripe backend error

## Webhook Error Handling

```csharp
public async Task ProcessWebhookAsync(string json, string signature)
{
    try
    {
        var stripeEvent = EventUtility.ConstructEvent(json, signature, secret);

        switch (stripeEvent.Type)
        {
            case "charge.failed":
                await HandleChargeFailedAsync((Charge)stripeEvent.Data.Object);
                break;

            case "charge.succeeded":
                await HandleChargeSucceededAsync((Charge)stripeEvent.Data.Object);
                break;

            default:
                _logger.LogWarning("Unhandled event type: {Type}", stripeEvent.Type);
                break;
        }
    }
    catch (StripeException ex)
    {
        _logger.LogError("Webhook processing error: {Code}", ex.StripeError.Code);
        throw; // Stripe will retry
    }
}
```

## Logging Best Practices

Do log:
- Error code
- Error type
- Stripe charge ID
- Timestamp
- User ID

Don't log:
- Card number
- CVV
- Full payment method details
- Customer email (for security)

```csharp
_logger.LogError(
    "Payment failed: code={ErrorCode}, charge={ChargeId}, trainer={TrainerId}",
    ex.StripeError.Code,
    ex.StripeError.Charge,
    trainerId);
```
