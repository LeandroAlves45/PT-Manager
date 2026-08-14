namespace Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs;

/// <summary>Solicita uma página de logs do cliente.</summary>
public sealed record ListExerciseSetLogsQuery(
    Guid ClientId,
    Guid? TrainingPlanId = null,
    DateTimeOffset? PerformedFrom = null,
    DateTimeOffset? PerformedTo = null,
    int PageNumber = 1,
    int PageSize = 50
);
