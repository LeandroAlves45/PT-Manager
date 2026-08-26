// create-payment-intent.cs
// Handler para criar Payment Intent com idempotência, retry e error handling

namespace Application.Billing.Commands;

using Domain.Billing;
using Infrastructure.Stripe;
using Stripe;

/// Request DTO
public class CreatePaymentIntentRequest
{
    public Guid ClientId { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "eur";
}

/// Response DTO
public class CreatePaymentIntentResponse
{
    public string ClientSecret { get; set; }
    public string PublishableKey { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; }
}

/// Handler (MediatR or similar)
public class CreatePaymentIntentHandler
{
    private readonly IStripeHttpClientWithRetry _httpClient;
    private readonly StripeClient _stripeClient;
    private readonly IIdempotencyLogRepository _idempotencyLog;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreatePaymentIntentHandler> _logger;
    private readonly string _stripePublishableKey;

    public CreatePaymentIntentHandler(
        IStripeHttpClientWithRetry httpClient,
        StripeClient stripeClient,
        IIdempotencyLogRepository idempotencyLog,
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        ILogger<CreatePaymentIntentHandler> logger,
        IConfiguration config)
    {
        _httpClient = httpClient;
        _stripeClient = stripeClient;
        _idempotencyLog = idempotencyLog;
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _logger = logger;
        _stripePublishableKey = config["Stripe:PublishableKey"];
    }

    public async Task<Result<CreatePaymentIntentResponse>> ExecuteAsync(
        CreatePaymentIntentRequest request,
        CancellationToken ct)
    {
        // 1. Validação de entrada
        if (request.AmountCents <= 0)
            return Result.Failure("Amount must be greater than 0");

        if (request.AmountCents > 999999999) // Max ~9.9M EUR
            return Result.Failure("Amount exceeds maximum limit");

        var trainerId = _tenantContext.GetTrainerId();
        var clientId = request.ClientId;
        var amountCents = request.AmountCents;

        // 2. Gerar chave de idempotência
        var idempotencyKey = GenerateIdempotencyKey(
            trainerId,
            clientId,
            amountCents);

        _logger.LogInformation(
            "Creating payment intent: trainer={TrainerId}, client={ClientId}, amount={Amount}",
            trainerId,
            clientId,
            amountCents);

        // 3. Verificar se já processado (idempotência no DB)
        var existing = await _idempotencyLog.GetAsync(idempotencyKey);
        if (existing != null)
        {
            _logger.LogInformation("Duplicate request (cached): {Key}", idempotencyKey);

            var cachedPayment = await _paymentRepository.GetByIdAsync(existing.StripePaymentIntentId);
            return Result.Success(new CreatePaymentIntentResponse
            {
                ClientSecret = cachedPayment.ClientSecret,
                PublishableKey = _stripePublishableKey,
                Amount = amountCents,
                Currency = request.Currency
            });
        }

        // 4. Chamar Stripe com retry
        var paymentIntent = await _httpClient.ExecuteWithRetryAsync(
            async () => await CreatePaymentIntentWithStripeAsync(
                trainerId,
                clientId,
                amountCents,
                request.Currency,
                idempotencyKey,
                ct),
            $"CreatePaymentIntent({trainerId}, {amountCents})");

        // 5. Guardar no DB
        var payment = new Payment
        {
            TrainerId = trainerId,
            ClientId = clientId,
            AmountCents = amountCents,
            Currency = request.Currency,
            StripePaymentIntentId = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _paymentRepository.AddAsync(payment);

        // 6. Log idempotência
        await _idempotencyLog.AddAsync(new IdempotencyLog
        {
            IdempotencyKey = idempotencyKey,
            Operation = "payment_intent_create",
            StripePaymentIntentId = paymentIntent.Id,
            ProcessedAt = DateTime.UtcNow
        });

        // 7. Retornar ao cliente
        return Result.Success(new CreatePaymentIntentResponse
        {
            ClientSecret = paymentIntent.ClientSecret,
            PublishableKey = _stripePublishableKey,
            Amount = amountCents,
            Currency = request.Currency
        });
    }

    private async Task<PaymentIntent> CreatePaymentIntentWithStripeAsync(
        Guid trainerId,
        Guid clientId,
        long amountCents,
        string currency,
        string idempotencyKey,
        CancellationToken ct)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency.ToLower(),
            PaymentMethodTypes = new List<string> { "card" },
            Metadata = new Dictionary<string, string>
            {
                { "trainer_id", trainerId.ToString() },
                { "client_id", clientId.ToString() }
            }
        };

        var requestOptions = new RequestOptions
        {
            IdempotencyKey = idempotencyKey
        };

        return await _stripeClient.PaymentIntents.CreateAsync(
            options,
            requestOptions,
            cancellationToken: ct);
    }

    private string GenerateIdempotencyKey(Guid trainerId, Guid clientId, long amountCents)
    {
        return $"ptmanager_{trainerId}_{clientId}_{amountCents}_payment_intent_create";
    }
}

/// Repository interfaces
public interface IIdempotencyLogRepository
{
    Task AddAsync(IdempotencyLog log);
    Task<IdempotencyLog> GetAsync(string idempotencyKey);
}

public interface IPaymentRepository
{
    Task AddAsync(Payment payment);
    Task<Payment> GetByIdAsync(string stripePaymentIntentId);
}

/// Domain entity
public class Payment
{
    public Guid TrainerId { get; set; }
    public Guid ClientId { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; }
    public string StripePaymentIntentId { get; set; }
    public string ClientSecret { get; set; }
    public string Status { get; set; } // pending, succeeded, failed
    public DateTime CreatedAt { get; set; }
}

public class IdempotencyLog
{
    public string IdempotencyKey { get; set; }
    public string Operation { get; set; }
    public string StripePaymentIntentId { get; set; }
    public DateTime ProcessedAt { get; set; }
}

/// Regras:
/// 1. Validação de amount (> 0, < max)
/// 2. Gerar idempotency key único
/// 3. Verificar cache no DB (evita Stripe call)
/// 4. Retry automático com exponential backoff
/// 5. Guardar no DB depois de sucesso
/// 6. NUNCA retornar secret key ao cliente
/// 7. NUNCA armazenar card data
/// 8. NUNCA enviar amount como decimal (usar cents = integer)
