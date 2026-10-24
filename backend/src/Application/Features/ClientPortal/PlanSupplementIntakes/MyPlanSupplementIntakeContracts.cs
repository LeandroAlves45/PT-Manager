namespace Application.Features.ClientPortal.PlanSupplementIntakes;

/// <summary>
/// Suplementos do plano alimentar que o cliente escolheu, com a toma de hoje no fuso do
/// personal trainer: os de cada refeição e os do plano fora das refeições.
/// </summary>
public sealed record MyPlanSupplementsTodayDto(
    Guid MealPlanId,
    string MealPlanName,
    DateOnly LocalDate,
    IReadOnlyList<MyPlanSupplementItemDto> Items);

/// <summary>
/// Prescrição de um suplemento no plano escolhido. <c>Placement</c> é <c>meal</c> (com o tipo e
/// a ordem da refeição) ou <c>plan</c> (fora das refeições). Indisponível quando o suplemento
/// foi arquivado no catálogo: aparece, mas a toma não se marca.
/// </summary>
public sealed record MyPlanSupplementItemDto(
    Guid Id,
    string Placement,
    string? MealType,
    int? MealOrderNumber,
    int OrderNumber,
    string SupplementName,
    string UnitOfMeasure,
    decimal Quantity,
    string? Notes,
    bool IsUnavailable,
    DateTime? TakenAt);

/// <summary>Toma de hoje de uma prescrição do plano.</summary>
public sealed record MyPlanSupplementIntakeDto(Guid ItemId, DateOnly LocalDate, DateTime TakenAt);

/// <summary>Estado da leitura das tomas do plano.</summary>
public enum MyPlanSupplementsReadStatus
{
    Found,
    ProfileNotAvailable,
    PlanNotSelected,
    PlanNotAvailable
}

/// <summary>Resultado da leitura das tomas do plano.</summary>
public sealed record MyPlanSupplementsRead(MyPlanSupplementsReadStatus Status, MyPlanSupplementsTodayDto? Today);

/// <summary>Lê os suplementos do plano escolhido e as tomas de um dia.</summary>
public interface IMyPlanSupplementIntakeQueries
{
    Task<MyPlanSupplementsRead> GetForDateAsync(
        Guid trainerId,
        Guid clientUserId,
        DateOnly localDate,
        CancellationToken cancellationToken);
}

/// <summary>Estados da escrita de uma toma do plano.</summary>
public enum MyPlanSupplementIntakeStatus
{
    Marked,
    AlreadyMarked,
    Unmarked,
    NotMarked,
    ProfileNotAvailable,
    ItemNotFound,
    ItemUnavailable
}

/// <summary>Resultado da escrita de uma toma do plano.</summary>
public sealed record MyPlanSupplementIntakeResult(
    MyPlanSupplementIntakeStatus Status,
    MyPlanSupplementIntakeDto? Intake);

/// <summary>
/// Marca e desmarca a toma de hoje de uma prescrição do plano escolhido. A prescrição tem de
/// pertencer ao plano que o cliente tem aberto; desmarcar é permitido num suplemento
/// entretanto indisponível.
/// </summary>
public interface IMyPlanSupplementIntakeStore
{
    Task<MyPlanSupplementIntakeResult> MarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid itemId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<MyPlanSupplementIntakeResult> UnmarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid itemId,
        DateTime now,
        CancellationToken cancellationToken);
}
