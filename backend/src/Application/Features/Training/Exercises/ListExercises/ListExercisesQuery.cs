namespace Application.Features.Training.Exercises.ListExercises;

/// <summary>Solicita uma página do catálogo de exercícios.</summary>
public sealed record ListExercisesQuery(
    string? Search = null,
    ExerciseActivityFilter Activity = ExerciseActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
