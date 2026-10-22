using Application.Features.ClientPortal.Abstractions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Grava o plano alimentar que o cliente escolheu. Uma só instrução confirma, no mesmo passo,
/// que o plano é do próprio cliente e está ativo: um identificador de outro cliente ou de um
/// plano arquivado não escreve nada.
/// </summary>
internal sealed class MyMealPlanSelectionStore : IMyMealPlanSelectionStore
{
    private readonly PtManagerDbContext _dbContext;

    public MyMealPlanSelectionStore(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<bool> SelectAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid mealPlanId,
        CancellationToken cancellationToken)
    {
        var affected = await _dbContext.Clients
            .Where(client =>
                client.OwnerTrainerId == trainerId &&
                client.UserId == clientUserId &&
                client.IsActive &&
                _dbContext.MealPlans.Any(plan =>
                    plan.Id == mealPlanId &&
                    plan.ClientId == client.Id &&
                    plan.OwnerTrainerId == trainerId &&
                    plan.IsActive &&
                    !plan.IsArchived))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    client => client.SelectedMealPlanId,
                    (Guid?)mealPlanId),
                cancellationToken);

        return affected == 1;
    }
}
