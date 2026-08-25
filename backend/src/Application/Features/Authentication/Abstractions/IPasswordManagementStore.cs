namespace Application.Features.Authentication.Abstractions;

/// <summary>Fronteira transacional das credenciais persistidas.</summary>
public interface IPasswordManagementStore
{
    Task<PasswordManagementStoreResult> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        DateTime now,
        CancellationToken cancellationToken);

    Task<PasswordManagementStoreResult> ResetAsync(
        string rawToken,
        string newPassword,
        DateTime now,
        CancellationToken cancellationToken);
}
