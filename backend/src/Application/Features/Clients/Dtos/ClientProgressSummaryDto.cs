namespace Application.Features.Clients.Dtos;

/// <summary>Resumo de progresso do cliente para o separador "Resumo" do personal trainer.</summary>
public sealed record ClientProgressSummaryDto(
    Guid ClientId,
    DateOnly LocalToday,
    ClientProgressSummaryDto.WeightDto? Weight,
    int? HeightCm,
    ClientProgressSummaryDto.TrainingPlanDto? TrainingPlan,
    ClientProgressSummaryDto.AdherenceDto? Adherence,
    ClientProgressSummaryDto.NutritionDto? Nutrition,
    ClientProgressSummaryDto.PacksDto Packs)
{
    /// <summary>Origem do peso atual.</summary>
    public const string WeightSourceCheckIn = "check_in";

    /// <summary>Origem do peso atual quando ainda não há check-ins respondidos.</summary>
    public const string WeightSourceInitialAssessment = "initial_assessment";

    /// <summary>Peso atual e variação face ao check-in mais antigo da janela.</summary>
    public sealed record WeightDto(
        decimal CurrentKg,
        DateOnly MeasuredOn,
        string Source,
        decimal? ChangeKg,
        DateOnly? ChangeSince);

    /// <summary>Plano de treino ativo do cliente.</summary>
    public sealed record TrainingPlanDto(
        Guid Id,
        string Name,
        DateOnly StartDate,
        DateOnly? EndDate,
        int DaysPerWeek);

    /// <summary>Adesão ao treino na janela (séries registadas + planeadas).</summary>
    public sealed record AdherenceDto(
        DateOnly WindowStart,
        DateOnly WindowEnd,
        int PlannedSets,
        int LoggedSets,
        int? Percentage);

    /// <summary>Metas do plano alimentar ativo do cliente.</summary>
    public sealed record NutritionDto(
        Guid MealPlanId,
        string Name,
        decimal TargetKcal,
        decimal ProteinTargetGrams,
        decimal CarbsTargetGrams,
        decimal FatsTargetGrams);

    /// <summary>Saldo de packs utilizavéis (sessões por usar).</summary>
    public sealed record PacksDto(
        int UsablePackCount,
        int SessionsRemaining,
        DateOnly? NextExpectedEndDate);
}
