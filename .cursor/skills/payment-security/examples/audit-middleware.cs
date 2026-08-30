// audit-middleware.cs
// Middleware para audit logging de operações de pagamento

namespace Infrastructure.Middleware;

using Domain.Billing;
using System.Text.Json;

/// Entity de audit log
public class PaymentAuditLog
{
    public int Id { get; set; }
    public Guid TrainerId { get; set; }
    public string EventType { get; set; }
    public string StripeEventId { get; set; }
    public string Status { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// Middleware para logging de pagamentos
public class PaymentAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IPaymentAuditRepository _auditRepository;
    private readonly ILogger<PaymentAuditMiddleware> _logger;

    public PaymentAuditMiddleware(
        RequestDelegate next,
        IPaymentAuditRepository auditRepository,
        ILogger<PaymentAuditMiddleware> logger)
    {
        _next = next;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        // 1. Interceptar apenas endpoints de pagamento
        if (!IsPaymentOperation(path))
        {
            await _next(context);
            return;
        }

        // 2. Preparar audit log
        var auditLog = new PaymentAuditLog
        {
            TrainerId = context.User.GetTrainerId(), // Multi-tenant
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers["User-Agent"].ToString(),
            EventType = ExtractEventType(context.Request.Method, path),
            CreatedAt = DateTime.UtcNow
        };

        // 3. Capturar response
        var originalBodyStream = context.Response.Body;
        using (var memoryStream = new MemoryStream())
        {
            context.Response.Body = memoryStream;

            // 4. Executar request
            await _next(context);

            // 5. Ler response (para extrair amount, status)
            memoryStream.Position = 0;
            using (var reader = new StreamReader(memoryStream))
            {
                var responseBody = await reader.ReadToEndAsync();

                // Tentar extrair amount (se resposta é JSON com payment data)
                try
                {
                    var jsonDoc = JsonDocument.Parse(responseBody);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("amount", out var amountElement))
                        if (long.TryParse(amountElement.GetRawText(), out var amount))
                            auditLog.AmountCents = amount;

                    if (root.TryGetProperty("currency", out var currencyElement))
                        auditLog.Currency = currencyElement.GetString();
                }
                catch
                {
                    // Ignorar se não conseguir fazer parse
                }
            }

            // 6. Guardar log
            auditLog.Status = context.Response.StatusCode.ToString();

            try
            {
                await _auditRepository.AddAsync(auditLog);

                _logger.LogInformation(
                    "Payment operation logged: {EventType}, trainer={TrainerId}, status={Status}, ip={IpAddress}",
                    auditLog.EventType,
                    auditLog.TrainerId,
                    auditLog.Status,
                    auditLog.IpAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save audit log");
                // Não falhar o request se audit log falhar
            }

            // 7. Copiar response de volta para output
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);
        }

        context.Response.Body = originalBodyStream;
    }

    /// Determinar se é operação de billing (subscrição/checkout)
    private bool IsPaymentOperation(string path)
    {
        return path.Contains("/api/v1/billing", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/webhooks/stripe", StringComparison.OrdinalIgnoreCase);
    }

    /// Extrair tipo de evento baseado em método + path
    private string ExtractEventType(string method, string path)
    {
        return (method, path.ToLower()) switch
        {
            ("POST", var p) when p.Contains("billing/checkout") => "checkout_created",
            ("GET", var p) when p.Contains("billing/subscription") => "subscription_retrieved",
            ("POST", var p) when p.Contains("webhooks/stripe") => "webhook_received",
            _ => "unknown"
        };
    }
}

/// Registar middleware em Program.cs
public static class PaymentAuditMiddlewareExtensions
{
    public static IApplicationBuilder UsePaymentAudit(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PaymentAuditMiddleware>();
    }
}

/// Repository para guardar audit logs
public interface IPaymentAuditRepository
{
    Task AddAsync(PaymentAuditLog log);
    Task<IEnumerable<PaymentAuditLog>> GetByTrainerAsync(Guid trainerId, int days = 30);
    Task<IEnumerable<PaymentAuditLog>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate);
}

/// Implementação EF Core
public class PaymentAuditRepository : IPaymentAuditRepository
{
    private readonly ApplicationDbContext _context;

    public async Task AddAsync(PaymentAuditLog log)
    {
        _context.PaymentAuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<PaymentAuditLog>> GetByTrainerAsync(Guid trainerId, int days = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return await _context.PaymentAuditLogs
            .Where(l => l.TrainerId == trainerId && l.CreatedAt >= cutoff)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<PaymentAuditLog>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _context.PaymentAuditLogs
            .Where(l => l.CreatedAt >= from && l.CreatedAt <= to)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate)
    {
        var deleted = await _context.PaymentAuditLogs
            .Where(l => l.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync();

        return deleted;
    }
}

/// Regras:
/// 1. Logar TODAS operações de pagamento (sem exceções)
/// 2. NUNCA guardar card data em logs
/// 3. Incluir: trainer ID, IP, user agent, timestamp, status
/// 4. Guardar 1 ano (compliance)
/// 5. Usar para auditoria + investigação de disputes
