using Application.Features.ClientPortal.PlanSupplementIntakes;

namespace Api.Contracts.Portal;

/// <summary>Suplementos do plano alimentar escolhido, com a toma de hoje.</summary>
public sealed record MyPlanSupplementsTodayResponse(
    Guid MealPlanId,
    string MealPlanName,
    DateOnly LocalDate,
    IReadOnlyList<MyPlanSupplementItemResponse> Items)
{
    /// <summary>Converte o DTO da Application.</summary>
    public static MyPlanSupplementsTodayResponse From(MyPlanSupplementsTodayDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyPlanSupplementsTodayResponse(
            dto.MealPlanId,
            dto.MealPlanName,
            dto.LocalDate,
            dto.Items.Select(MyPlanSupplementItemResponse.From).ToArray());
    }
}

/// <summary>
/// Prescrição de um suplemento no plano: <c>placement</c> é <c>meal</c> (com a refeição) ou
/// <c>plan</c> (fora das refeições). <c>taken_at</c> nulo quando a toma de hoje não está marcada.
/// </summary>
public sealed record MyPlanSupplementItemResponse(
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
    DateTime? TakenAt)
{
    /// <summary>Converte o DTO da Application.</summary>
    public static MyPlanSupplementItemResponse From(MyPlanSupplementItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyPlanSupplementItemResponse(
            dto.Id,
            dto.Placement,
            dto.MealType,
            dto.MealOrderNumber,
            dto.OrderNumber,
            dto.SupplementName,
            dto.UnitOfMeasure,
            dto.Quantity,
            dto.Notes,
            dto.IsUnavailable,
            dto.TakenAt);
    }
}

/// <summary>Toma de hoje de uma prescrição do plano.</summary>
public sealed record MyPlanSupplementIntakeResponse(Guid ItemId, DateOnly LocalDate, DateTime TakenAt)
{
    /// <summary>Converte o DTO da Application.</summary>
    public static MyPlanSupplementIntakeResponse From(MyPlanSupplementIntakeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyPlanSupplementIntakeResponse(dto.ItemId, dto.LocalDate, dto.TakenAt);
    }
}
