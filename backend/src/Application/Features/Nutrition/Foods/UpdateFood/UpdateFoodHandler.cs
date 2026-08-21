using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.UpdateFood;

/// <summary>Atualiza um alimento privado sem permitir escrita no catálogo global.</summary>
public sealed class UpdateFoodHandler
{
    private readonly IValidator<UpdateFoodCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IFoodStore _foodStore;

    public UpdateFoodHandler(
        IValidator<UpdateFoodCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IFoodStore foodStore
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _foodStore = foodStore ?? throw new ArgumentNullException(nameof(foodStore));
    }

    public async Task<Result<FoodDto>> HandleAsync(
        UpdateFoodCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<FoodDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<FoodDto>.Failure(actor.Error!);

        var outcome = await _foodStore.UpdateAsync(
            command.FoodId,
            actor.Value.TrainerId,
            command.Name,
            command.Description,
            command.Protein,
            command.Carbs,
            command.Fats,
            command.Fiber,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToUpdateResult();
    }
}
