using Application.Features.Clients.Dtos;

namespace Api.Contracts.Clients;

/// <summary>Peso atual e variação na janela de 56 dias.</summary>
public sealed record ClientWeightSummaryResponse(
    decimal CurrentKg,
    DateOnly MeasuredOn,
    string Source,
    decimal? ChangeKg,
    DateOnly? ChangeSince)
{
    public static ClientWeightSummaryResponse From(ClientProgressSummaryDto.WeightDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientWeightSummaryResponse(
            dto.CurrentKg,
            dto.MeasuredOn,
            dto.Source,
            dto.ChangeKg,
            dto.ChangeSince);
    }
}

/// <summary>Plano de treino ativo do cliente.</summary>
public sealed record ClientTrainingPlanSummaryResponse(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    int DaysPerWeek)
{
    public static ClientTrainingPlanSummaryResponse From(ClientProgressSummaryDto.TrainingPlanDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientTrainingPlanSummaryResponse(
            dto.Id,
            dto.Name,
            dto.StartDate,
            dto.EndDate,
            dto.DaysPerWeek);
    }
}

/// <summary>Adesão ao treino na janela de 28 dias.</summary>
public sealed record ClientAdherenceResponse(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int PlannedSets,
    int LoggedSets,
    int? Percentage)
{
    public static ClientAdherenceResponse From(ClientProgressSummaryDto.AdherenceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientAdherenceResponse(
            dto.WindowStart, dto.WindowEnd, dto.PlannedSets, dto.LoggedSets, dto.Percentage);
    }
}

/// <summary>Metas do plano alimentar ativo.</summary>
public sealed record ClientNutritionTargetResponse(
    Guid MealPlanId,
    string Name,
    decimal TargetKcal,
    decimal ProteinTargetGrams,
    decimal CarbsTargetGrams,
    decimal FatsTargetGrams)
{
    public static ClientNutritionTargetResponse From(ClientProgressSummaryDto.NutritionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientNutritionTargetResponse(
            dto.MealPlanId,
            dto.Name,
            dto.TargetKcal,
            dto.ProteinTargetGrams,
            dto.CarbsTargetGrams,
            dto.FatsTargetGrams);
    }
}

/// <summary>Saldo de packs utilizáveis.</summary>
public sealed record ClientPackBalanceResponse(
    int UsablePackCount,
    int SessionsRemaining,
    DateOnly? NextExpectedEndDate)
{
    public static ClientPackBalanceResponse From(ClientProgressSummaryDto.PacksDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientPackBalanceResponse(
            dto.UsablePackCount, dto.SessionsRemaining, dto.NextExpectedEndDate);
    }
}

/// <summary>Resumo de progresso do cliente para o separador "Resumo".</summary>
public sealed record ClientSummaryOverviewResponse(
    Guid ClientId,
    DateOnly LocalToday,
    ClientWeightSummaryResponse? Weight,
    int? HeightCm,
    ClientTrainingPlanSummaryResponse? TrainingPlan,
    ClientAdherenceResponse? Adherence,
    ClientNutritionTargetResponse? Nutrition,
    ClientPackBalanceResponse Packs)
{
    public static ClientSummaryOverviewResponse From(ClientProgressSummaryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ClientSummaryOverviewResponse(
            dto.ClientId,
            dto.LocalToday,
            dto.Weight is null ? null : ClientWeightSummaryResponse.From(dto.Weight),
            dto.HeightCm,
            dto.TrainingPlan is null ? null : ClientTrainingPlanSummaryResponse.From(dto.TrainingPlan),
            dto.Adherence is null ? null : ClientAdherenceResponse.From(dto.Adherence),
            dto.Nutrition is null ? null : ClientNutritionTargetResponse.From(dto.Nutrition),
            ClientPackBalanceResponse.From(dto.Packs));
    }
}
