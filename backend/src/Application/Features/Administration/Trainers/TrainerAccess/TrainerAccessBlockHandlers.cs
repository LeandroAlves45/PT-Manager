using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Administration.Trainers.TrainerAccess;

/// <summary>Bloqueia a conta de um personal trainer com um dos quatro motivos e uma nota opcional.</summary>
public sealed record BlockTrainerCommand(Guid TrainerId, string ReasonCode, string? Note = null);

/// <summary>Levanta o bloqueio da conta de um personal trainer.</summary>
public sealed record UnblockTrainerCommand(Guid TrainerId);

/// <summary>Valida o identificador, a allowlist do motivo e a nota livre.</summary>
public sealed class BlockTrainerCommandValidator : AbstractValidator<BlockTrainerCommand>
{
    public BlockTrainerCommandValidator()
    {
        RuleFor(command => command.TrainerId)
            .NotEmpty()
            .WithErrorCode("trainer_id_required");

        RuleFor(command => command.ReasonCode)
            .Must(PlatformEnforcementReason.IsSupported)
            .WithErrorCode("platform_enforcement_reason_invalid");

        RuleFor(command => command.Note)
            .MaximumLength(PlatformEnforcementNotes.MaxLength)
            .WithErrorCode("platform_enforcement_note_too_long")
            .WithoutNullCharacter();
    }
}

/// <summary>
/// Bloqueia o personal trainer (RN-013): deixa de entrar nesse momento, os convites de cliente
/// por aceitar deixam de servir e os clientes com conta continuam no portal durante 7 dias.
/// Os emails saem pelo outbox, gravado na mesma transação. Bloquear quem já está bloqueado
/// mantém o bloqueio original (os 7 dias contam desde o primeiro momento).
/// </summary>
public sealed class BlockTrainerHandler
{
    private readonly IValidator<BlockTrainerCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationQueries _queries;
    private readonly ITrainerAdministrationStore _store;
    private readonly IClock _clock;

    public BlockTrainerHandler(
        IValidator<BlockTrainerCommand> validator,
        ITenantContext tenantContext,
        ITrainerAdministrationQueries queries,
        ITrainerAdministrationStore store,
        IClock clock)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<AdminTrainerDto>> HandleAsync(
        BlockTrainerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<AdminTrainerDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<AdminTrainerDto>.Failure(actor.Error!);

        var status = await _store.BlockAccessAsync(
            actor.Value.UserId,
            command.TrainerId,
            PlatformEnforcementReason.FromString(command.ReasonCode),
            command.Note,
            _clock.UtcNow,
            cancellationToken);

        return await TrainerAccessResults.ToResultAsync(
            status, command.TrainerId, _queries, cancellationToken);
    }
}

/// <summary>
/// Levanta o bloqueio (RN-013): o acesso do personal trainer e dos clientes volta, esteja o
/// prazo a meio ou já passado, e os dois recebem email. Levantar sem bloqueio não faz nada.
/// </summary>
public sealed class UnblockTrainerHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationQueries _queries;
    private readonly ITrainerAdministrationStore _store;
    private readonly IClock _clock;

    public UnblockTrainerHandler(
        ITenantContext tenantContext,
        ITrainerAdministrationQueries queries,
        ITrainerAdministrationStore store,
        IClock clock)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<AdminTrainerDto>> HandleAsync(
        UnblockTrainerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<AdminTrainerDto>.Failure(actor.Error!);

        var status = await _store.UnblockAccessAsync(
            actor.Value.UserId,
            command.TrainerId,
            _clock.UtcNow,
            cancellationToken);

        return await TrainerAccessResults.ToResultAsync(
            status, command.TrainerId, _queries, cancellationToken);
    }
}

internal static class TrainerAccessResults
{
    internal static async Task<Result<AdminTrainerDto>> ToResultAsync(
        TrainerAdministrationStoreStatus status,
        Guid trainerId,
        ITrainerAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        var failure = status switch
        {
            TrainerAdministrationStoreStatus.Changed or
            TrainerAdministrationStoreStatus.Unchanged => null,
            TrainerAdministrationStoreStatus.NotFound => TrainerAdministrationErrors.TrainerNotFound,
            TrainerAdministrationStoreStatus.ActorInvalid => TrainerAdministrationErrors.AdministratorOnly,
            TrainerAdministrationStoreStatus.ConcurrencyConflict =>
                TrainerAdministrationErrors.ConcurrencyConflict,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        if (failure is not null)
            return Result<AdminTrainerDto>.Failure(failure);

        var trainer = await queries.GetTrainerAsync(trainerId, cancellationToken);
        return trainer is null
            ? Result<AdminTrainerDto>.Failure(TrainerAdministrationErrors.TrainerNotFound)
            : Result<AdminTrainerDto>.Success(trainer);
    }
}
