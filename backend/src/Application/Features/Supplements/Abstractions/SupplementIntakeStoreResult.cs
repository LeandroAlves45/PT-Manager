using Domain.Entities.Supplements;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Classifico o resultado de marcar ou desmarcar uma toma.</summary>
public sealed record SupplementIntakeStoreResult
{
    public enum Status
    {
        Marked,
        AlreadyMarked,
        Unmarked,
        NotMarked,
        AssignmentNotFound,
        AssignmentInactive
    }

    public Status Kind { get; }
    public ClientSupplementIntake? Intake { get; }

    private SupplementIntakeStoreResult(Status kind, ClientSupplementIntake? intake)
    {
        Kind = kind;
        Intake = intake;
    }

    public bool IsSuccess => Kind is Status.Marked or Status.Unmarked;

    public static SupplementIntakeStoreResult ForMarked(ClientSupplementIntake intake) =>
        new(Status.Marked, intake ?? throw new ArgumentNullException(nameof(intake)));

    public static SupplementIntakeStoreResult ForAlreadyMarked(ClientSupplementIntake intake) =>
        new(Status.AlreadyMarked, intake ?? throw new ArgumentNullException(nameof(intake)));

    public static SupplementIntakeStoreResult For(Status kind)
    {
        if (kind is Status.Marked or Status.AlreadyMarked)
            throw new ArgumentException("Marked results require the intake.", nameof(kind));

        return new(kind, null);
    }
}
