using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Errors;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Results;

namespace Application.Features.Administration.ContentModeration.UnblockFood;

/// <summary>Remove o bloqueio sem alterar a disponibilidade escolhida pelo personal trainer.</summary>
public sealed class UnblockFoodHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPrivateCatalogModerationStore _store;

    public UnblockFoodHandler(
        ITenantContext tenantContext,
        IClock clock,
        IPrivateCatalogModerationStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        UnblockFoodCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FoodId == Guid.Empty)
            return Result.Failure(Error.Validation([
                new ValidationError(
                    "FoodId",
                    "food_id_required",
                    "Food ID is required."
                )
            ]));

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            ContentModerationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        return (await _store.UnblockFoodAsync(
            actor.Value.UserId,
            command.FoodId,
            _clock.UtcNow,
            cancellationToken))
            .ToResult();
    }
}
