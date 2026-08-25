namespace Application.Features.Authentication.Abstractions;

/// <summary>Emite credenciais descartáveis de reset de password.</summary>
public interface IPasswordResetRequestStore
{
    Task<PasswordResetRequestStoreResult> IssueAsync(
        string email,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken);
}
