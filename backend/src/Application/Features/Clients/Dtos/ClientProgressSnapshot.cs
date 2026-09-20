namespace Application.Features.Clients.Dtos;

/// <summary>
/// Dados crus do resumo lidos numa só passagem pela Infrastructure. O handler é quem aplica
/// o fuso do personal trainer (datas locais dos registos) e quem calcula a adesão, para manter a
/// regra de negócio fora das queries.
/// </summary>
public sealed record ClientProgressSnapshot(
    Guid ClientId,
    CheckInWeightRow? LatestCheckInWeight,
    CheckInWeightRow? EarliestCheckInWeightInWindow,
    InitialAssessmentRow? InitialAssessment,
    ActiveTrainingPlanStructure? ActiveTrainingPlan,
    IReadOnlyList<PerformedSetRow> SetLogs,
    ClientProgressSummaryDto.NutritionDto? ActiveMealPlan,
    ClientProgressSummaryDto.PacksDto Packs);

/// <summary>Peso respondido num check-in.</summary>
public sealed record CheckInWeightRow(DateOnly CheckedInDate, decimal WeightKg);

/// <summary>Métricas da avaliação inicial.</summary>
public sealed record InitialAssessmentRow(decimal WeightKg, int HeightCm, DateTime CreatedAt);

/// <summary>Estrutura mínima do plano ativo necessária para o calendário e a adesão.</summary>
public sealed record ActiveTrainingPlanStructure(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyList<PlannedDayRow> Days);

/// <summary>Dia do plano com as séries prescritas.</summary>
public sealed record PlannedDayRow(
    int WeekNumber,
    int DayOfWeek,
    IReadOnlyList<PlannedSetRow> Sets);

/// <summary>Série prescrita, identificada por prescrição e número.</summary>
public sealed record PlannedSetRow(Guid PrescriptionId, int SetNumber);

/// <summary>Série registada pelo cliente, ainda em UTC.</summary>
public sealed record PerformedSetRow(Guid PrescriptionId, int SetNumber, DateTimeOffset PerformedAt);
