using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Administration.ContentModeration.BlockExercise;

/// <summary>Bloqueia um Exercise privado através do store administrativo dedicado.</summary>
public sealed class BlockExerciseHandler
{
    private readonly IValidator<BlockExerciseCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPrivateCatalogModerationStore _store;

    public BlockExerciseHandler(
        IValidator<BlockExerciseCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IPrivateCatalogModerationStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(BlockExerciseCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            ContentModerationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.BlockExerciseAsync(
            actor.Value.UserId,
            command.ExerciseId,
            PlatformEnforcementReason.FromString(command.ReasonCode),
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToResult();
    }
}
