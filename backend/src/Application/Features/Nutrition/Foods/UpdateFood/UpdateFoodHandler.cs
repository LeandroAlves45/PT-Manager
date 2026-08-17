using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(foodStore);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _foodStore = foodStore;
    }

    public async Task<Result<FoodDto>> HandleAsync(
        UpdateFoodCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<FoodDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<FoodDto>.Failure(tenant.Error!);

        var outcome = await _foodStore.UpdateAsync(
            command.FoodId,
            tenant.Value,
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
