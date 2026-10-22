namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Série registada hoje pelo cliente autenticado.</summary>
public sealed record MyTodaySetLogRow(
    Guid LogId,
    Guid PrescriptionId,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    decimal? Rpe,
    DateTimeOffset PerformedAt,
    string? Notes);

/// <summary>Conclusão de um dia de treino, com a nota que o cliente escreveu ao concluir.</summary>
public sealed record MyWorkoutCompletionRow(DateTime CompletedAt, string? Notes);

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

    /// <summary>Conclusão do dia indicado, se existir.</summary>
    Task<MyWorkoutCompletionRow?> GetMyCompletionAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanDayId,
        DateOnly localDate,
        CancellationToken cancellationToken);
}
