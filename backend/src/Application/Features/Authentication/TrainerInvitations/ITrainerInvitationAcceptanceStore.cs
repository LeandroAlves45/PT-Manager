namespace Application.Features.Authentication.TrainerInvitations;

/// <summary>Leitura e consumo anónimos do convite de treinador isento.</summary>
public interface ITrainerInvitationAcceptanceStore
{
    Task<TrainerInvitationStatusResult> GetStatusAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cria o treinador com o email confirmado e a subscrição Pro isenta, e marca o convite como
    /// usado, tudo na mesma transação.
    /// </summary>
    Task<TrainerInvitationStatus> RegisterAsync(
        string rawToken,
        string fullName,
        string password,
        DateTime now,
        CancellationToken cancellationToken);
}

public enum TrainerInvitationStatus
{
    Valid,
    Registered,
    Invalid,
    Expired,
    Used,
    AccountAlreadyExists,
    PasswordRejected,
    ConcurrencyConflict
}

/// <summary>O email só é devolvido a quem tem um link válido.</summary>
public sealed record TrainerInvitationStatusResult(TrainerInvitationStatus Status, string? Email);
