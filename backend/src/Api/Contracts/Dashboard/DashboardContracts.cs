using Application.Features.Dashboard.Dtos;

namespace Api.Contracts.Dashboard;

/// <summary>Check-in por rever no Dashboard.</summary>
public sealed record PendingReviewCheckInResponse(
    Guid CheckInId,
    Guid ClientId,
    string ClientName,
    DateOnly CheckInDate,
    DateTime RespondedAt)
{
    public static PendingReviewCheckInResponse From(TrainerDashboardDto.PendingReviewItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PendingReviewCheckInResponse(
            dto.CheckInId,
            dto.ClientId,
            dto.ClientName,
            dto.CheckInDate,
            dto.RespondedAt);
    }
}

/// <summary>Bloco "check-ins por rever" no Dashboard.</summary>
public sealed record CheckInsPendingReviewResponse(
    int TotalCount,
    int OverdueCount,
    IReadOnlyList<PendingReviewCheckInResponse> Items)
{
    public static CheckInsPendingReviewResponse From(TrainerDashboardDto.CheckInsPendingReviewDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new CheckInsPendingReviewResponse(
            dto.TotalCount,
            dto.OverdueCount,
            dto.Items.Select(PendingReviewCheckInResponse.From).ToList());
    }
}

/// <summary>Pack a terminar.</summary>
public sealed record EndingPackResponse(
    Guid PackId,
    Guid ClientId,
    string ClientName,
    string PackName,
    int SessionsTotal,
    int SessionsRemaining,
    DateOnly? ExpectedEndDate)
{
    /// <summary>Converte o DTO da Application.</summary>
    public static EndingPackResponse From(TrainerDashboardDto.EndingPackItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new EndingPackResponse(
            dto.PackId,
            dto.ClientId,
            dto.ClientName,
            dto.PackName,
            dto.SessionsTotal,
            dto.SessionsRemaining,
            dto.ExpectedEndDate);
    }
}

/// <summary>Bloco "packs de sessões a terminar".</summary>
public sealed record PacksEndingResponse(int TotalCount, IReadOnlyList<EndingPackResponse> Items)
{
    public static PacksEndingResponse From(TrainerDashboardDto.PacksEndingDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PacksEndingResponse(
            dto.TotalCount,
            dto.Items.Select(EndingPackResponse.From).ToList());
    }
}

/// <summary>Plano a expirar.</summary>
public sealed record ExpiringPlanResponse(
    Guid PlanId,
    Guid ClientId,
    string ClientName,
    string PlanName,
    DateOnly EndDate)
{
    public static ExpiringPlanResponse From(TrainerDashboardDto.ExpiringPlanItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ExpiringPlanResponse(
            dto.PlanId,
            dto.ClientId,
            dto.ClientName,
            dto.PlanName,
            dto.EndDate);
    }
}

/// <summary>Bloco "planos a expirar" (treino e alimentares).</summary>
public sealed record PlansExpiringResponse(
    int TotalCount,
    int TrainingPlanCount,
    int MealPlanCount,
    IReadOnlyList<ExpiringPlanResponse> TrainingPlans,
    IReadOnlyList<ExpiringPlanResponse> MealPlans)
{
    public static PlansExpiringResponse From(TrainerDashboardDto.PlansExpiringDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PlansExpiringResponse(
            dto.TotalCount,
            dto.TrainingPlanCount,
            dto.MealPlanCount,
            dto.TrainingPlans.Select(ExpiringPlanResponse.From).ToList(),
            dto.MealPlans.Select(ExpiringPlanResponse.From).ToList());
    }
}

/// <summary>Sessão de hoje.</summary>
public sealed record TodaySessionResponse(
    Guid SessionId,
    Guid ClientId,
    string ClientName,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? SessionType,
    string? Location,
    string Status)
{
    public static TodaySessionResponse From(TrainerDashboardDto.TodaySessionItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new TodaySessionResponse(
            dto.SessionId,
            dto.ClientId,
            dto.ClientName,
            dto.StartsAt,
            dto.DurationMinutes,
            dto.SessionType,
            dto.Location,
            dto.Status);
    }
}

/// <summary>Bloco "sessões de hoje".</summary>
public sealed record SessionsTodayResponse(
    int TotalCount,
    DateTimeOffset? NextSessionStartsAt,
    IReadOnlyList<TodaySessionResponse> Items)
{
    public static SessionsTodayResponse From(TrainerDashboardDto.SessionsTodayDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new SessionsTodayResponse(
            dto.TotalCount,
            dto.NextSessionStartsAt,
            dto.Items.Select(TodaySessionResponse.From).ToList());
    }
}

/// <summary>Cliente sem plano de treino ativo.</summary>
public sealed record ClientWithoutTrainingPlanResponse(
    Guid ClientId,
    string ClientName,
    bool HasHadPlan,
    DateOnly WithoutPlanSince,
    int DaysWithoutPlan)
{
    public static ClientWithoutTrainingPlanResponse From(
        TrainerDashboardDto.ClientWithoutPlanItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientWithoutTrainingPlanResponse(
            dto.ClientId,
            dto.ClientName,
            dto.HasHadPlan,
            dto.WithoutPlanSince,
            dto.DaysWithoutPlan);
    }
}

/// <summary>Bloco "clientes sem plano activo".</summary>
public sealed record ClientsWithoutTrainingPlanResponse(
    int TotalCount,
    IReadOnlyList<ClientWithoutTrainingPlanResponse> Items)
{
    public static ClientsWithoutTrainingPlanResponse From(
        TrainerDashboardDto.ClientsWithoutTrainingPlanDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientsWithoutTrainingPlanResponse(
            dto.TotalCount,
            dto.Items.Select(ClientWithoutTrainingPlanResponse.From).ToList());
    }
}

/// <summary>Total de vendas de packs numa moeda.</summary>
public sealed record PackSalesTotalResponse(string Currency, long AmountCents, int PackCount)
{
    public static PackSalesTotalResponse From(TrainerDashboardDto.PackSalesTotalDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PackSalesTotalResponse(dto.Currency, dto.AmountCents, dto.PackCount);
    }
}

/// <summary>Vendas de packs de um mês local.</summary>
public sealed record PackSalesMonthResponse(
    int Year,
    int Month,
    IReadOnlyList<PackSalesTotalResponse> Totals)
{
    public static PackSalesMonthResponse From(TrainerDashboardDto.PackSalesMonthDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PackSalesMonthResponse(
            dto.Year,
            dto.Month,
            dto.Totals.Select(PackSalesTotalResponse.From).ToList());
    }
}

/// <summary>Bloco "vendas de packs (estimado)".</summary>
public sealed record PackSalesResponse(
    PackSalesMonthResponse CurrentMonth,
    PackSalesMonthResponse PreviousMonth)
{
    public static PackSalesResponse From(TrainerDashboardDto.PackSalesDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PackSalesResponse(
            PackSalesMonthResponse.From(dto.CurrentMonth),
            PackSalesMonthResponse.From(dto.PreviousMonth));
    }
}

/// <summary>Dashboard agregado do personal trainer.</summary>
public sealed record TrainerDashboardResponse(
    DateOnly LocalToday,
    int ActiveClientCount,
    CheckInsPendingReviewResponse CheckInsPendingReview,
    PacksEndingResponse PacksEnding,
    PlansExpiringResponse PlansExpiring,
    SessionsTodayResponse SessionsToday,
    ClientsWithoutTrainingPlanResponse ClientsWithoutTrainingPlan,
    PackSalesResponse PackSales)
{
    public static TrainerDashboardResponse From(TrainerDashboardDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new TrainerDashboardResponse(
            dto.LocalToday,
            dto.ActiveClientCount,
            CheckInsPendingReviewResponse.From(dto.CheckInsPendingReview),
            PacksEndingResponse.From(dto.PacksEnding),
            PlansExpiringResponse.From(dto.PlansExpiring),
            SessionsTodayResponse.From(dto.SessionsToday),
            ClientsWithoutTrainingPlanResponse.From(dto.ClientsWithoutTrainingPlan),
            PackSalesResponse.From(dto.PackSales));
    }
}
