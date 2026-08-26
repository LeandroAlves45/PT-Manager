// webhook-processor.cs
// Processador seguro de webhooks Stripe com idempotência

namespace Infrastructure.Stripe;

using Stripe;

/// Entity para rastrear webhooks processados
public class WebhookLog
{
    public int Id { get; set; }
    public string StripeEventId { get; set; }
    public string EventType { get; set; }
    public string Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string ErrorMessage { get; set; }
}

/// Interface para processador de webhooks
public interface IStripeWebhookProcessor
{
    Task<bool> ProcessWebhookAsync(string json, string signature);
}

/// Implementação do processador
public class StripeWebhookProcessor : IStripeWebhookProcessor
{
    private readonly IPaymentAuditRepository _auditRepository;
    private readonly IWebhookLogRepository _webhookLogRepository;
    private readonly ILogger<StripeWebhookProcessor> _logger;
    private readonly string _webhookSecret;

    public StripeWebhookProcessor(
        IPaymentAuditRepository auditRepository,
        IWebhookLogRepository webhookLogRepository,
        ILogger<StripeWebhookProcessor> logger,
        IConfiguration config)
    {
        _auditRepository = auditRepository;
        _webhookLogRepository = webhookLogRepository;
        _logger = logger;
        _webhookSecret = config["Stripe:WebhookSecret"];
    }

    public async Task<bool> ProcessWebhookAsync(string json, string signature)
    {
        Event stripeEvent = null;

        try
        {
            // 1. Verificar assinatura webhook
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signature,
                _webhookSecret);

            _logger.LogInformation("Webhook received: {EventId} - {Type}", stripeEvent.Id, stripeEvent.Type);

            // 2. Verificar idempotência (mesmo evento já processado?)
            var existingLog = await _webhookLogRepository.GetByStripeEventIdAsync(stripeEvent.Id);
            if (existingLog?.Status == "succeeded")
            {
                _logger.LogInformation("Duplicate webhook (already processed): {EventId}", stripeEvent.Id);
                return true;
            }

            // 3. Processar evento baseado em tipo
            var result = await HandleWebhookEventAsync(stripeEvent);

            // 4. Logar sucesso
            await _webhookLogRepository.AddAsync(new WebhookLog
            {
                StripeEventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                Status = "succeeded",
                ProcessedAt = DateTime.UtcNow
            });

            return result;
        }
        catch (StripeException ex)
        {
            _logger.LogError("Webhook signature verification failed: {Message}", ex.Message);

            // Log assinatura inválida
            if (stripeEvent != null)
            {
                await _webhookLogRepository.AddAsync(new WebhookLog
                {
                    StripeEventId = stripeEvent.Id,
                    EventType = stripeEvent.Type,
                    Status = "failed",
                    ErrorMessage = $"Signature verification failed: {ex.Message}",
                    ProcessedAt = DateTime.UtcNow
                });
            }

            return false; // Stripe vai retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook processing error");

            if (stripeEvent != null)
            {
                await _webhookLogRepository.AddAsync(new WebhookLog
                {
                    StripeEventId = stripeEvent.Id,
                    EventType = stripeEvent.Type,
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    ProcessedAt = DateTime.UtcNow
                });
            }

            return false; // Stripe vai retry
        }
    }

    /// Processar evento baseado em tipo
    private async Task<bool> HandleWebhookEventAsync(Event stripeEvent)
    {
        return stripeEvent.Type switch
        {
            "charge.succeeded" => await HandleChargeSucceededAsync((Charge)stripeEvent.Data.Object),
            "charge.failed" => await HandleChargeFailedAsync((Charge)stripeEvent.Data.Object),
            "charge.refunded" => await HandleChargeRefundedAsync((Charge)stripeEvent.Data.Object),
            "payment_intent.requires_action" => await HandlePaymentIntentRequiresActionAsync((PaymentIntent)stripeEvent.Data.Object),
            "payment_intent.succeeded" => await HandlePaymentIntentSucceededAsync((PaymentIntent)stripeEvent.Data.Object),
            _ => true // Ignore eventos desconhecidos
        };
    }

    private async Task<bool> HandleChargeSucceededAsync(Charge charge)
    {
        _logger.LogInformation("Processing charge.succeeded: {ChargeId}", charge.Id);

        // Lógica para pagamento bem-sucedido
        // 1. Marcar pagamento como completado
        // 2. Criar fatura
        // 3. Enviar email de confirmação
        // 4. Audit log

        await _auditRepository.AddAsync(new PaymentAuditLog
        {
            EventType = "charge_succeeded",
            StripeEventId = charge.Id,
            Status = "succeeded",
            AmountCents = charge.Amount ?? 0,
            Currency = charge.Currency,
            CreatedAt = DateTime.UtcNow
        });

        return true;
    }

    private async Task<bool> HandleChargeFailedAsync(Charge charge)
    {
        _logger.LogWarning("Processing charge.failed: {ChargeId} - {FailureCode}", charge.Id, charge.FailureCode);

        // Lógica para pagamento falhou
        // 1. Marcar como falhou
        // 2. Armazenar razão de falha
        // 3. Notificar utilizador
        // 4. Audit log

        await _auditRepository.AddAsync(new PaymentAuditLog
        {
            EventType = "charge_failed",
            StripeEventId = charge.Id,
            Status = charge.FailureCode,
            AmountCents = charge.Amount ?? 0,
            Currency = charge.Currency,
            CreatedAt = DateTime.UtcNow
        });

        return true;
    }

    private async Task<bool> HandleChargeRefundedAsync(Charge charge)
    {
        _logger.LogInformation("Processing charge.refunded: {ChargeId}", charge.Id);

        // Lógica para reembolso
        // 1. Atualizar status para refunded
        // 2. Criar credit memo
        // 3. Notificar utilizador

        return await Task.FromResult(true);
    }

    private async Task<bool> HandlePaymentIntentRequiresActionAsync(PaymentIntent paymentIntent)
    {
        _logger.LogInformation("Processing payment_intent.requires_action: {PaymentIntentId}", paymentIntent.Id);

        // 3D Secure ou outro tipo de autenticação necessário
        // Notificar utilizador para completar autenticação

        return await Task.FromResult(true);
    }

    private async Task<bool> HandlePaymentIntentSucceededAsync(PaymentIntent paymentIntent)
    {
        _logger.LogInformation("Processing payment_intent.succeeded: {PaymentIntentId}", paymentIntent.Id);

        // Pagamento completado end-to-end

        return await Task.FromResult(true);
    }
}

/// Repository para WebhookLog
public interface IWebhookLogRepository
{
    Task AddAsync(WebhookLog log);
    Task<WebhookLog> GetByStripeEventIdAsync(string stripeEventId);
}

/// Regras:
/// 1. SEMPRE verificar X-Stripe-Signature
/// 2. SEMPRE verificar idempotência (evt_xxx único)
/// 3. NUNCA processar sem signature check
/// 4. NUNCA guardar card data
/// 5. NUNCA throw exceptions (return false para Stripe retry)
/// 6. Logar sem dados sensíveis
