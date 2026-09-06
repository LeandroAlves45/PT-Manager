namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Persite o hash de um nonce externo associado a um finalidade.</summary>
public interface IExternalChallengeStore
{
    Task<IssuedExternalChallenge> IssueAsync(
        string purpose,
        Guid? userId,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken);
}
