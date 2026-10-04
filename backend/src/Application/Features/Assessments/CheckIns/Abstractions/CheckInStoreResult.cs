using Domain.Entities.Assessments;

namespace Application.Features.Assessments.CheckIns.Abstractions;

/// <summary>Resultado esperado de uma mutação de check-in.</summary>
public sealed class CheckInStoreResult
{
    public enum Status
    {
        Created,
        Rescheduled,
        Cancelled,
        Answered,
        Corrected,
        Reviewed,
        AlreadyInRequestedState,
        ClientNotFound,
        ClientInactive,
        CheckInNotFound,
        DateConflict,
        DateNotAllowed,
        CannotReschedule,
        CannotCancel,
        WrongResponseDay,
        AlreadyAnswered,
        CheckInCancelled,
        NotAnswered
    }

    public Status Kind { get; }
    public CheckIn? CheckIn { get; }

    /// <summary>Nome do cliente, preenchido sempre que <see cref="CheckIn"/> o está.</summary>
    public string? ClientName { get; }

    private CheckInStoreResult(Status kind, CheckIn? checkIn, string? clientName)
    {
        Kind = kind;
        CheckIn = checkIn;
        ClientName = clientName;
    }

    /// <summary>Resultado sem check-in (falhas de regra ou de existência).</summary>
    public static CheckInStoreResult For(Status kind) => new(kind, null, null);

    /// <summary>
    /// Resultado com o check-in resultante. O nome do cliente é obrigatório: a resposta
    /// devolve <c>client_name</c> e o store já tem o cliente bloqueado nesse ponto.
    /// </summary>
    public static CheckInStoreResult For(Status kind, CheckIn checkIn, string clientName)
    {
        ArgumentNullException.ThrowIfNull(checkIn);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        return new(kind, checkIn, clientName);
    }
}
