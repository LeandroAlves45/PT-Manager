namespace Application.Features.Dashboard.Dtos;

/// <summary>
/// Dados lidos pela Infrastructure. Só difere do DTO final nos blocos que precisas de fuso:
/// o "sem plano desde" chega em UTC e as vendas chegam por mês; o handler converte ambos.
/// </summary>
public sealed record TrainerDashboardData(
    int ActiveClientCount,
    TrainerDashboardDto.CheckInsPendingReviewDto CheckInsPendingReview,
    TrainerDashboardDto.PacksEndingDto PacksEnding,
    TrainerDashboardDto.PlansExpiringDto PlansExpiring,
    TrainerDashboardDto.SessionsTodayDto SessionsToday,
    int ClientsWithoutTrainingPlanCount,
    IReadOnlyList<ClientWithoutPlanRow> ClientsWithoutTrainingPlan,
    IReadOnlyList<PackSalesRow> PackSales);

/// <summary>Cliente sem plano, com o instante(UTC) desde quando está sem plano.</summary>
public sealed record ClientWithoutPlanRow(
    Guid ClientId,
    string ClientName,
    bool HasHadPlan,
    DateTime WithoutPlanSinceUtc);

/// <summary>Soma das vendas agrupada por mês local de compra e moeda.</summary>
public sealed record PackSalesRow(
    DateOnly MonthStart,
    string Currency,
    long AmountCents,
    int PackCount);
