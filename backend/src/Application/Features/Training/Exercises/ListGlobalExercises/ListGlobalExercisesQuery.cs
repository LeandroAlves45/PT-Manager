namespace Application.Features.Training.Exercises.ListGlobalExercises;

/// <summary>Pesquisa paginada do catálogo de exercícios globais.</summary>
public sealed record ListGlobalExercisesQuery(
    string? Search,
    GlobalExerciseActivityFilter Activity = GlobalExerciseActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
