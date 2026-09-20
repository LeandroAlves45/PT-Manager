namespace Application.Features.Dashboard.Dtos;

/// <summary>Dashboard agregado ao personal trainer (só leitura, só clientes ativos).</summary>
public sealed record TrainerDashboardDto(
    DateOnly LocalToday,
    int ActiveClientCount,
    TrainerDashboardDto.CheckInsPendingReviewDto CheckInsPendingReview,
    TrainerDashboardDto.PacksEndingDto PacksEnding,
    TrainerDashboardDto.PlansExpiringDto PlansExpiring,
    TrainerDashboardDto.SessionsTodayDto SessionsToday,
    TrainerDashboardDto.ClientsWithoutTrainingPlanDto ClientsWithoutTrainingPlan,
    TrainerDashboardDto.PackSalesDto PackSales)
{
    /// <summary>Check-ins respondidos, não cancelados e ainda não revistos.</summary>
    public sealed record CheckInsPendingReviewDto(
        int TotalCount,
        int OverdueCount,
        IReadOnlyList<PendingReviewItemDto> Items);

    /// <summary>Check-in por rever.</summary>
    public sealed record PendingReviewItemDto(
        Guid CheckInId,
        Guid ClientId,
        string ClientName,
        DateOnly CheckInDate,
        DateTime RespondedAt);

    /// <summary>Packs utilizáveis com poucas sessões ou fim previsto próximo.</summary>
    public sealed record PacksEndingDto(int TotalCount, IReadOnlyList<EndingPackItemDto> Items);

    /// <summary>Packs a terminar.</summary>
    public sealed record EndingPackItemDto(
        Guid PackId,
        Guid ClientId,
        string ClientName,
        string PackName,
        int SessionsTotal,
        int SessionsRemaining,
        DateOnly? ExpectedEndDate);

    /// <summary>Planos ativos cuja data de fim cai entre hoje e hoje + 7 dias.</summary>
    public sealed record PlansExpiringDto(
        int TotalCount,
        int TrainingPlanCount,
        int MealPlanCount,
        IReadOnlyList<ExpiringPlanItemDto> TrainingPlans,
        IReadOnlyList<ExpiringPlanItemDto> MealPlans);

    /// <summary>Plano a expirar.</summary>
    public sealed record ExpiringPlanItemDto(
        Guid PlanId,
        Guid ClientId,
        string ClientName,
        string PlanName,
        DateOnly EndDate);

    /// <summary>Sessões do dia local do personal trainer.</summary>
    public sealed record SessionsTodayDto(
        int TotalCount,
        DateTimeOffset? NextSessionStartsAt,
        IReadOnlyList<TodaySessionItemDto> Items);

    /// <summary>Sessões de hoje.</summary>
    public sealed record TodaySessionItemDto(
        Guid SessionId,
        Guid ClientId,
        string ClientName,
        DateTimeOffset StartsAt,
        int DurationMinutes,
        string? SessionType,
        string? Location,
        string Status);

    /// <summary>Clientes ativos sem plano de treino ativo.</summary>
    public sealed record ClientsWithoutTrainingPlanDto(
        int TotalCount,
        IReadOnlyList<ClientWithoutPlanItemDto> Items);

    /// <summary>Cliente sem plano de treino ativo.</summary>
    public sealed record ClientWithoutPlanItemDto(
        Guid ClientId,
        string ClientName,
        bool HasHadPlan,
        DateOnly WithoutPlanSince,
        int DaysWithoutPlan);

    /// <summary>Vendas de packs estimadas (não são pagamentos de clientes).</summary>
    public sealed record PackSalesDto(
        PackSalesMonthDto CurrentMonth,
        PackSalesMonthDto PreviousMonth);

    /// <summary>Totais de um mês local, por moeda.</summary>
    public sealed record PackSalesMonthDto(
        int Year,
        int Month,
        IReadOnlyList<PackSalesTotalDto> Totals);

    /// <summary>Soma de PriceCents dos packs comprados no mês, numa moeda.</summary>
    public sealed record PackSalesTotalDto(
        string Currency,
        long AmountCents,
        int PackCount);
}
