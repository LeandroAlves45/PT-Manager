# Webhook Signature Verification

PT Manager deve SEMPRE verificar X-Stripe-Signature antes de processar webhooks.

## Approach 1: Stripe SDK (Recomendado)

```csharp
var json = await context.Request.Body.ReadAsStringAsync();
var signature = context.Request.Headers["Stripe-Signature"];

try
{
    var stripeEvent = EventUtility.ConstructEvent(
        json,
        signature,
        webhookSecret);

    // Agora stripeEvent é validado e seguro
    // Processar o evento
}
catch (StripeException ex)
{
    // Assinatura inválida → rejeitar
    _logger.LogError($"Invalid webhook signature: {ex}");
    return Unauthorized();
}
```

## Approach 2: Manual HMAC-SHA256

Se não usar Stripe SDK (não recomendado):

```csharp
using System.Security.Cryptography;

public bool VerifyWebhookSignature(string json, string signature, string secret)
{
    // Extrair timestamp + hash da assinatura
    var parts = signature.Split(',');
    var timestamp = parts[0].Split('=')[1];
    var sentHash = parts[1].Split('=')[1];

    // Computar expected hash
    var signedContent = $"{timestamp}.{json}";
    using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
    {
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        var expected = BitConverter.ToString(computedHash).Replace("-", "").ToLower();

        // Timing-safe comparison (previne timing attacks)
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(sentHash));
    }
}
```

## Critical Points

1. Nunca processar webhook sem signature check
2. Usar Stripe SDK quando possível (simpler, less error-prone)
3. Timing-safe comparison (protege contra timing attacks)
4. Replay attack prevention: check timestamp (max 5 min old)
5. Log all signature failures

## Replay Attack Prevention

```csharp
var timestamp = long.Parse(parts[0].Split('=')[1]);
var maxAge = DateTime.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

if (timestamp < maxAge)
{
    _logger.LogWarning("Replay attack detected: old timestamp");
    return BadRequest();
}
```

## Webhook Secret Management

- Stripe dashboard → Webhooks → Signing secret
- Store in environment variable: `STRIPE_WEBHOOK_SECRET`
- Never commit to git
- Rotate keys annually (Stripe → Reveal signing secret → Create new)
