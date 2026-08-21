using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Nutrition;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.CreateFood;

/// <summary>Cria um alimento pertencente ao tenant autenticado.</summary>
public sealed class CreateFoodHandler
{
    private readonly IValidator<CreateFoodCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IFoodStore _foodStore;

    public CreateFoodHandler(
        IValidator<CreateFoodCommand> validator,
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
        CreateFoodCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<FoodDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<FoodDto>.Failure(actor.Error!);

        var food = new Food(
            actor.Value.TrainerId,
            command.Name,
            command.Description,
            command.Protein,
            command.Carbs,
            command.Fats,
            command.Fiber,
            _clock.UtcNow
        );

        await _foodStore.AddAsync(food, cancellationToken);

        // A recarga obtém Kcal da coluna GENERATED depois do commit.
        var persisted = await _foodStore.GetOwnedForReadAsync(food.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "A committed Food must be readable by its owner."
            );

        return Result<FoodDto>.Success(persisted.ToDto());
    }
}
