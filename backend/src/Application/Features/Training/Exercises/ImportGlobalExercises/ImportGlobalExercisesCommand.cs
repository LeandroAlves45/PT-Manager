namespace Application.Features.Training.Exercises.ImportGlobalExercises;

/// <summary>
/// Ficheiro CSV de exercícios globais. Com <see cref="DryRun"/> só valida e conta, sem gravar.
/// </summary>
public sealed record ImportGlobalExercisesCommand(Stream Content, bool DryRun);
