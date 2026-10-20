namespace Application.Features.Administration.Overview;

/// <summary>
/// Contagens da visão geral do superuser. Só números: nenhum dado pessoal e
/// nenhum contéudo de tenant é exposto.
/// </summary>
public sealed record AdminOverviewDto(
    AdminOverviewDto.CatalogCountsDto GlobalFoods,
    AdminOverviewDto.CatalogCountsDto GlobalExercises,
    AdminOverviewDto.CatalogCountsDto GlobalSupplements,
    AdminOverviewDto.ModerationCountsDto PrivateFoods,
    AdminOverviewDto.ModerationCountsDto PrivateExercises,
    AdminOverviewDto.TrainerCountsDto Trainers)
{
    public sealed record CatalogCountsDto(int TotalCount, int ActiveCount, int ArchivedCount);

    public sealed record ModerationCountsDto(int TotalCount, int BlockedCount);

    /// <summary>
    /// Painel da plataforma (RN-018): contagens do estado de agora e o líquido recebido no mês
    /// de calendário em Europe/Lisbon. Os isentos não contam como subscrição ativa paga.
    /// </summary>
    public sealed record TrainerCountsDto(
        int ActiveSubscriptionCount,
        int SuspendedSubscriptionCount,
        int ExemptCount,
        long RevenueThisMonthCents);
}
