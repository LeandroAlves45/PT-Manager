using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.Email;

/// <summary>Categoria técnica de uma chamada ao endpoint de email.</summary>
internal enum ResendTransportOutcomeKind
{
    Sent,
    TransientFailure,
    PermanentFailure
}

/// <summary>Resultado sanitizado do transporte Resend.</summary>
internal sealed record ResendTransportOutcome(
    ResendTransportOutcomeKind Kind,
    string? FailureCode,
    int? StatusCode)
{
    public static ResendTransportOutcome Sent() =>
        new(ResendTransportOutcomeKind.Sent, null, null);

    public static ResendTransportOutcome Transient(string code, int? statusCode = null) =>
        new(ResendTransportOutcomeKind.TransientFailure, code, statusCode);

    public static ResendTransportOutcome Permanent(string code, int statusCode) =>
        new(ResendTransportOutcomeKind.PermanentFailure, code, statusCode);
}

/// <summary>Payload comum aceite por 'POST /emails'.</summary>
internal sealed record ResendEmailMessage(
    string From,
    IReadOnlyList<string> To,
    string Subject,
    string Html,
    string Text);

/// <summary>Executa chamadas Resend sem registar conteúdo sensível.</summary>
internal static class ResendEmailTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Envia um email e classifica apenas informação segura.</summary>
    public static async Task<ResendTransportOutcome> SendAsync(
        HttpClient httpClient,
        ResendEmailMessage message,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(message);
        ValidateMessage(message);
        ValidateIdempotencyKey(idempotencyKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(message, options: JsonOptions)
        };

        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
                return ResendTransportOutcome.Sent();

            var statusCode = (int)response.StatusCode;
            var failureCode = $"resend_http_{statusCode}";
            return IsTransient(response.StatusCode)
                ? ResendTransportOutcome.Transient(failureCode, statusCode)
                : ResendTransportOutcome.Permanent(failureCode, statusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResendTransportOutcome.Transient("resend_timeout");
        }
        catch (HttpRequestException)
        {
            return ResendTransportOutcome.Transient("resend_unreachable");
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.Conflict or
            HttpStatusCode.TooManyRequests ||
        (int)statusCode == 425 ||
        (int)statusCode >= 500;

    private static void ValidateMessage(ResendEmailMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.From) ||
            message.To.Count != 1 ||
            string.IsNullOrWhiteSpace(message.To[0]) ||
            string.IsNullOrWhiteSpace(message.Subject) ||
            string.IsNullOrWhiteSpace(message.Html) ||
            string.IsNullOrWhiteSpace(message.Text))
            throw new ArgumentException("Resend email message is incomplete.", nameof(message));
    }

    private static void ValidateIdempotencyKey(string? idempotencyKey)
    {
        if (idempotencyKey is null)
            return;

        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > 256 ||
            idempotencyKey.Any(character =>
                character is '\r' or '\n' || character > 127))
            throw new ArgumentException("Resend idempotency key is invalid.", nameof(idempotencyKey));
    }
}
