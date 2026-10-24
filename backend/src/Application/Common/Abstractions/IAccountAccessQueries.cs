namespace Application.Common.Abstractions;

/// <summary>
/// Confirma, a cada pedido autenticado e em cada entrada, que a conta ainda pode usar a app.
/// É o que faz uma sessão já aberta deixar de servir no momento em que o cliente é arquivado
/// ou o personal trainer é bloqueado, em vez de esperar que o token expire.
/// </summary>
public interface IAccountAccessQueries
{
    Task<AccountAccessState> GetAsync(
        Guid userId,
        string role,
        Guid? trainerId,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>Momento do bloqueio ativo do personal trainer, ou null sem bloqueio.</summary>
    Task<DateTime?> GetTrainerBlockedAtAsync(Guid trainerId, CancellationToken cancellationToken);
}

/// <summary>Estado de acesso de uma conta autenticada.</summary>
public enum AccountAccessStatus
{
    /// <summary>A conta pode continuar.</summary>
    Allowed,

    /// <summary>A sessão acabou sem explicação (conta inativa ou cliente arquivado).</summary>
    Ended,

    /// <summary>O personal trainer está bloqueado; vê o motivo e a nota.</summary>
    TrainerBlocked,

    /// <summary>O cliente de um personal trainer bloqueado passou os dias de acesso.</summary>
    ClientAccessPeriodEnded
}

/// <summary>Resultado da verificação de acesso; o motivo e a nota só existem para o bloqueado.</summary>
public sealed record AccountAccessState(
    AccountAccessStatus Status,
    string? BlockReason = null,
    string? BlockNote = null)
{
    public static readonly AccountAccessState Allowed = new(AccountAccessStatus.Allowed);
    public static readonly AccountAccessState Ended = new(AccountAccessStatus.Ended);
    public static readonly AccountAccessState ClientAccessPeriodEnded =
        new(AccountAccessStatus.ClientAccessPeriodEnded);
}
