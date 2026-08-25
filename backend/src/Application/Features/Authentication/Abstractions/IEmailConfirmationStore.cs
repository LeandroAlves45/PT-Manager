namespace Application.Features.Authentication.Abstractions;

/// <summary>Gere confirmações de email descartáveis.</summary>
public interface IEmailConfirmationStore
{
    Task<EmailConfirmationStoreResult> IssueAsync(
        Guid userId,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken);

    Task<EmailConfirmationStoreResult> ConsumeAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken);
}
