using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Administration;

/// <summary>
/// Bloqueio da conta de um personal trainer pelo superuser. Enquanto existe, o personal
/// trainer não entra; os clientes com conta continuam no portal durante
/// <see cref="ClientGraceDays"/> dias desde o bloqueio e depois também deixam de entrar.
/// Levantar o bloqueio apaga a linha (a auditoria administrativa guarda o histórico).
/// </summary>
public sealed class TrainerAccessBlock
{
    /// <summary>Dias em que os clientes continuam a usar o portal depois do bloqueio.</summary>
    public const int ClientGraceDays = 7;

    public Guid TrainerId { get; private set; }
    public PlatformEnforcementReason Reason { get; private set; } = null!;
    public string? Note { get; private set; }
    public DateTime BlockedAt { get; private set; }
    public Guid BlockedByUserId { get; private set; }

    /// <summary>Até quando os clientes entram no portal.</summary>
    public DateTime ClientAccessEndsAt => BlockedAt.AddDays(ClientGraceDays);

    private TrainerAccessBlock() { }

    public TrainerAccessBlock(
        Guid trainerId,
        PlatformEnforcementReason reason,
        string? note,
        Guid blockedByUserId,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(reason);
        if (trainerId == Guid.Empty || blockedByUserId == Guid.Empty)
            throw new DomainException("Trainer and blocking user are required.");

        TrainerId = trainerId;
        Reason = reason;
        Note = PlatformEnforcementNotes.Normalize(note);
        BlockedByUserId = blockedByUserId;
        BlockedAt = now;
    }

    /// <summary>Indica se, neste instante, os clientes já perderam o acesso.</summary>
    public static bool ClientAccessEnded(DateTime blockedAt, DateTime now) =>
        now >= blockedAt.AddDays(ClientGraceDays);
}
