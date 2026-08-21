using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.CreateGlobalFood;

/// <summary>Cria um alimento global através de um caso administrativo dedicado.</summary>
public sealed class CreateGlobalFoodHandler
{
    private readonly IValidator<CreateGlobalFoodCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalFoodStore _store;

    public CreateGlobalFoodHandler(
        IValidator<CreateGlobalFoodCommand> validator,
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
        CreateGlobalFoodCommand command,
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

        var outcome = await _store.CreateAsync(
            actor.Value.UserId,
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
