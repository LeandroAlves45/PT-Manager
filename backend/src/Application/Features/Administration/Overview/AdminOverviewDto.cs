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
    AdminOverviewDto.ModerationCountsDto PrivateExercises)
{
    public sealed record CatalogCountsDto(int TotalCount, int ActiveCount, int ArchivedCount);

    public sealed record ModerationCountsDto(int TotalCount, int BlockedCount);
}
