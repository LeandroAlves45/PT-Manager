using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;


namespace Infrastructure.Persistence.Training;

/// <summary>
/// Lock relacional da raiz do plano de treino, partilhado por quem escreve histórico
/// (séries e conclusões) e por quem altera a estrutura.
/// </summary>
internal static class TrainingPlanLocks
{
    /// <summary>
    /// Tranca a linha do plano (<c>FOR UPDATE</c>) e devolve-o sem tracking; null quando
    /// não existe, pertence a outro tenant ou foi eliminado. Exige transação aberta.
    /// </summary>
    internal static async Task<TrainingPlan?> LockAndLoadAsync(
        PtManagerDbContext dbContext,
        Guid planId,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        // TrainingPlanStore usa exatamente esta linha como mutex relacional.
        var lockedId = await dbContext.Database.SqlQuery<Guid>(
            $"SELECT id AS \"Value\" FROM training_plans WHERE id = {planId} AND owner_trainer_id = {trainerId} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lockedId == Guid.Empty)
            return null;

        return await dbContext.TrainingPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan =>
                plan.Id == lockedId && plan.OwnerTrainerId == trainerId,
                cancellationToken);
    }

    /// <summary>Indica se a data local está dentro do intervalo do plano.</summary>
    internal static bool Contains(TrainingPlan plan, DateOnly localDate) =>
        localDate >= plan.StartDate && (!plan.EndDate.HasValue || localDate <= plan.EndDate.Value);

}
