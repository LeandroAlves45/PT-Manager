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

    private CheckInStoreResult(Status kind, CheckIn? checkIn)
    {
        Kind = kind;
        CheckIn = checkIn;
    }

    public static CheckInStoreResult For(
        Status kind,
        CheckIn? checkIn = null
    ) => new(kind, checkIn);
}
