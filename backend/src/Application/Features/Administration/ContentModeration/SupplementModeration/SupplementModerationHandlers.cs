using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Errors;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Administration.ContentModeration.SupplementModeration;

/// <summary>Bloqueia um suplemento privado com um dos quatro motivos e uma nota opcional.</summary>
public sealed record BlockSupplementCommand(Guid SupplementId, string ReasonCode, string? Note);

/// <summary>Levanta o bloqueio de um suplemento privado.</summary>
public sealed record UnblockSupplementCommand(Guid SupplementId);

/// <summary>Valida o identificador, a allowlist do motivo e a nota.</summary>
public sealed class BlockSupplementCommandValidator : AbstractValidator<BlockSupplementCommand>
{
    public BlockSupplementCommandValidator()
    {
        RuleFor(command => command.SupplementId)
            .NotEmpty()
            .WithErrorCode("supplement_id_required");

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
/// Bloqueia um suplemento privado. Não tira o acesso a ninguém nem envia email: sai do
/// portal do cliente e fica marcado no plano do personal trainer, com o motivo e a nota.
/// </summary>
public sealed class BlockSupplementHandler
{
    private readonly IValidator<BlockSupplementCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPrivateCatalogModerationStore _store;

    public BlockSupplementHandler(
        IValidator<BlockSupplementCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IPrivateCatalogModerationStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(BlockSupplementCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(_tenantContext, ContentModerationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        return (await _store.BlockSupplementAsync(
                actor.Value.UserId,
                command.SupplementId,
                PlatformEnforcementReason.FromString(command.ReasonCode),
                command.Note,
                _clock.UtcNow,
                cancellationToken))
            .ToResult();
    }
}

/// <summary>
/// Levanta o bloqueio: o suplemento volta a ser utilizável na biblioteca e sai dos planos onde
/// estava; o cliente não o vê até o personal trainer o voltar a pôr.
/// </summary>
public sealed class UnblockSupplementHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPrivateCatalogModerationStore _store;

    public UnblockSupplementHandler(
        ITenantContext tenantContext,
        IClock clock,
        IPrivateCatalogModerationStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(UnblockSupplementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.SupplementId == Guid.Empty)
            return Result.Failure(Error.Validation([
                new ValidationError("SupplementId", "supplement_id_required", "Supplement ID is required.")
            ]));

        var actor = ActorAuthorization.RequireAdministrator(_tenantContext, ContentModerationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        return (await _store.UnblockSupplementAsync(
                actor.Value.UserId,
                command.SupplementId,
                _clock.UtcNow,
                cancellationToken))
            .ToResult();
    }
}
