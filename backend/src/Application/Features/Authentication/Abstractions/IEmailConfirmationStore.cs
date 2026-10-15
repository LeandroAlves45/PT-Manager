namespace Application.Features.Authentication.Abstractions;

/// <summary>Gere confirmações de email descartáveis.</summary>
public interface IEmailConfirmationStore
{
    Task<EmailConfirmationStoreResult> IssueAsync(
        Guid userId,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Emite a confirmação para o utilizador com este email, serializando pedidos
    /// concorrentes pelo email normalizado. Usa os mesmos estados de <see cref="IssueAsync"/>.
    /// </summary>
    Task<EmailConfirmationStoreResult> IssueByEmailAsync(
        string email,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken);

    Task<EmailConfirmationStoreResult> ConsumeAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken);
}
