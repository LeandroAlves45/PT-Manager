using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.UpdateGlobalFood;

/// <summary>Atualiza um alimento global e grava snapshots de auditoria.</summary>
public sealed class UpdateGlobalFoodHandler
{
    private readonly IValidator<UpdateGlobalFoodCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalFoodStore _store;

    public UpdateGlobalFoodHandler(
        IValidator<UpdateGlobalFoodCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IGlobalFoodStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<GlobalFoodDto>> HandleAsync(
        UpdateGlobalFoodCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<GlobalFoodDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, NutritionErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<GlobalFoodDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateAsync(
            actor.Value.UserId,
            command.FoodId,
            command.Name,
            command.Description,
            command.Protein,
            command.Carbs,
            command.Fats,
            command.Fiber,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToDtoResult();
    }
}
