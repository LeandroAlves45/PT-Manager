using Application.Features.Administration.Overview;

namespace Api.Contracts.Administration;

/// <summary>Contagens de um catálogo global.</summary>
public sealed record CatalogCountsResponse(int TotalCount, int ActiveCount, int ArchivedCount)
{
    public static CatalogCountsResponse From(AdminOverviewDto.CatalogCountsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new CatalogCountsResponse(
            dto.TotalCount, dto.ActiveCount, dto.ArchivedCount);
    }
}

/// <summary>Contagens de uma fila de conteúdo privado.</summary>
public sealed record ModerationCountsResponse(int TotalCount, int BlockedCount)
{
    public static ModerationCountsResponse From(AdminOverviewDto.ModerationCountsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ModerationCountsResponse(dto.TotalCount, dto.BlockedCount);
    }
}

/// <summary>Contagens de treinadores e o líquido recebido este mês, em cêntimos de euro.</summary>
public sealed record TrainerCountsResponse(
    int ActiveSubscriptionCount,
    int SuspendedSubscriptionCount,
    int ExemptCount,
    int BlockedAccountCount,
    long RevenueThisMonthCents)
{
    public static TrainerCountsResponse From(AdminOverviewDto.TrainerCountsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new TrainerCountsResponse(
            dto.ActiveSubscriptionCount,
            dto.SuspendedSubscriptionCount,
            dto.ExemptCount,
            dto.BlockedAccountCount,
            dto.RevenueThisMonthCents);
    }
}

/// <summary>Visão geral da plataforma para o superuser.</summary>
public sealed record AdminOverviewResponse(
    CatalogCountsResponse GlobalFoods,
    CatalogCountsResponse GlobalExercises,
    CatalogCountsResponse GlobalSupplements,
    ModerationCountsResponse PrivateFoods,
    ModerationCountsResponse PrivateExercises,
    ModerationCountsResponse PrivateSupplements,
    TrainerCountsResponse Trainers)
{
    public static AdminOverviewResponse From(AdminOverviewDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new AdminOverviewResponse(
            CatalogCountsResponse.From(dto.GlobalFoods),
            CatalogCountsResponse.From(dto.GlobalExercises),
            CatalogCountsResponse.From(dto.GlobalSupplements),
            ModerationCountsResponse.From(dto.PrivateFoods),
            ModerationCountsResponse.From(dto.PrivateExercises),
            ModerationCountsResponse.From(dto.PrivateSupplements),
            TrainerCountsResponse.From(dto.Trainers));
    }
}
