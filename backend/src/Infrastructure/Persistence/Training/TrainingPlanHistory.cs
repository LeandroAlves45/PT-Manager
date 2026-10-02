using Infrastructure.Data;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Regra única do que conta como histórico de um plano de treino: séries registadas
/// ou treinos concluídos. Uma conclusão sem séries (treino parcial) também conta,
/// porque referencia o dia com FK Restrict.
/// </summary>
/// <remarks>
/// A leitura (<c>has_history</c>) e o guard de escrita (409
/// <c>training_structure_has_history</c>) usam esta mesma consulta, para que a UI
/// nunca anuncie como editável um plano que o servidor vai recusar.
/// </remarks>
internal static class TrainingPlanHistory
{
    /// <summary>IDs de planos com histórico, composáveis noutra consulta EF.</summary>
    internal static IQueryable<Guid> PlanIdsWithHistory(PtManagerDbContext dbContext) =>
        (from log in dbContext.ClientExerciseSetLogs
         join exercise in dbContext.TrainingPlanDayExercises
             on log.TrainingPlanDayExerciseId equals exercise.Id
         join day in dbContext.TrainingPlanDays
             on exercise.TrainingPlanDayId equals day.Id
         select day.TrainingPlanId)
        .Concat(dbContext.WorkoutCompletions.Select(completion => completion.TrainingPlanId));
}
