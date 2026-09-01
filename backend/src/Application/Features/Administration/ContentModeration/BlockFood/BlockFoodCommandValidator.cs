using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Administration.ContentModeration.BlockFood;

/// <summary>Valida o identificador e a allowlist do motivo.</summary>
public sealed class BlockFoodCommandValidator : AbstractValidator<BlockFoodCommand>
{
    public BlockFoodCommandValidator()
    {
        RuleFor(command => command.FoodId)
            .NotEmpty()
            .WithErrorCode("food_id_required");

        RuleFor(command => command.ReasonCode)
            .Must(PlatformEnforcementReason.IsSupported)
            .WithErrorCode("platform_enforcement_reason_invalid");
    }
}
