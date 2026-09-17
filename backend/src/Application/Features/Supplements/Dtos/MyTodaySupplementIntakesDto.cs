namespace Application.Features.Supplements.Dtos;

/// <summary>
/// Estado das tomas de hoje: "X de Y tomadas". Sem noção de atraso, porque o horário
/// (Timing) é texto livre do personal trainer.
/// </summary>
public sealed record MyTodaySupplementIntakesDto(
    DateOnly LocalDate,
    int TakenCount,
    int TotalCount,
    IReadOnlyList<MyTodaySupplementIntakesDto.ItemDto> Items)
{
    /// <summary>Atribuição ativa e respetiva toma de hoje, se existir.</summary>
    public sealed record ItemDto(
        Guid AssignmentId,
        string SupplementName,
        string ServingSize,
        string Timing,
        bool IsTaken,
        DateTime? TakenAt);
}
