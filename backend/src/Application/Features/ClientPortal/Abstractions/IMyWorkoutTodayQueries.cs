namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Série registada hoje pelo cliente autenticado.</summary>
public sealed record MyTodaySetLogRow(
    Guid LogId,
    Guid PrescriptionId,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    decimal? Rpe,
    DateTimeOffset PerformedAt);

/// <summary>Leitura do treino de hoje do cliente autenticado.</summary>
public interface IMyWorkoutTodayQueries
{
    /// <summary>
    /// Lista as séries registadas pelo cliente no intervalo UTC do dia local, limitadas às
    /// prescrições do plano indicado.
    /// </summary>
    Task<IReadOnlyList<MyTodaySetLogRow>> ListMyLogsAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<DateTime?> GetMyCompletionAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanId,
        DateOnly localDate,
        CancellationToken cancellationToken);
}
