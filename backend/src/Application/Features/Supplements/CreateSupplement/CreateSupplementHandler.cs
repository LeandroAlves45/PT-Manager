using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Supplements;
using FluentValidation;

namespace Application.Features.Supplements.CreateSupplement;

/// <summary>Cria um suplemento privado para o personal trainer autenticado.</summary>
public sealed class CreateSupplementHandler
{
    private readonly IValidator<CreateSupplementCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISupplementStore _store;

    public CreateSupplementHandler(
        IValidator<CreateSupplementCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ISupplementStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<SupplementDto>> HandleAsync(
        CreateSupplementCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<SupplementDto>.Failure(validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<SupplementDto>.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var supplement = new Supplement(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.Name,
            command.Description,
            command.UnitOfMeasure,
            command.ServingSize,
            command.Timing,
            command.TrainerNotes,
            now
        );

        await _store.AddAsync(supplement, cancellationToken);
        return Result<SupplementDto>.Success(supplement.ToDto());
    }
}
