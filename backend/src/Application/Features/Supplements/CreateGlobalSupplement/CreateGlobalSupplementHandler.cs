using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.CreateGlobalSupplement;

/// <summary>Cria um suplemento global através de um caso administrativo dedicado.</summary>
public sealed class CreateGlobalSupplementHandler
{
    private readonly IValidator<CreateGlobalSupplementCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalSupplementStore _store;

    public CreateGlobalSupplementHandler(
        IValidator<CreateGlobalSupplementCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IGlobalSupplementStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<GlobalSupplementDto>> HandleAsync(
        CreateGlobalSupplementCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<GlobalSupplementDto>.Failure(validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireAdministrator(_tenantContext);
        if (!actor.IsSuccess)
            return Result<GlobalSupplementDto>.Failure(actor.Error!);

        var outcome = await _store.CreateAsync(
            actor.Value.UserId,
            command.Name,
            command.Description,
            command.UnitOfMeasure,
            command.ServingSize,
            command.Timing,
            command.TrainerNotes,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToDtoResult();
    }
}
