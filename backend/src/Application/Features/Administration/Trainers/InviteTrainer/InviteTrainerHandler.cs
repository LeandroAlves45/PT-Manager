using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Administration.Trainers.InviteTrainer;

public sealed record InviteTrainerCommand(string? Email);

public sealed class InviteTrainerCommandValidator : AbstractValidator<InviteTrainerCommand>
{
    public InviteTrainerCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("admin_trainer_email_required")
            .Must(EmailAddress.IsValid)
            .WithErrorCode("admin_trainer_email_invalid");
    }
}

/// <summary>
/// Envia o convite isento a um email sem conta (RN-008, FL-005). Enviar outro para o mesmo
/// email anula o anterior na mesma transação.
/// </summary>
public sealed class InviteTrainerHandler
{
    private readonly IValidator<InviteTrainerCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationStore _store;
    private readonly IOpaqueTokenService _tokens;
    private readonly IAuthenticationEmailSender _emailSender;
    private readonly IClock _clock;

    public InviteTrainerHandler(
        IValidator<InviteTrainerCommand> validator,
        ITenantContext tenantContext,
        ITrainerAdministrationStore store,
        IOpaqueTokenService tokens,
        IAuthenticationEmailSender emailSender,
        IClock clock)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<PendingTrainerInvitationDto>> HandleAsync(
        InviteTrainerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<PendingTrainerInvitationDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<PendingTrainerInvitationDto>.Failure(actor.Error!);

        var generated = _tokens.Generate();
        var issued = await _store.IssueInvitationAsync(
            actor.Value.UserId,
            command.Email!.Trim(),
            generated.TokenHash,
            _clock.UtcNow,
            cancellationToken);

        if (issued.Status != TrainerAdministrationStoreStatus.Changed || issued.Invitation is null)
        {
            return Result<PendingTrainerInvitationDto>.Failure(issued.Status switch
            {
                TrainerAdministrationStoreStatus.EmailAlreadyRegistered =>
                    TrainerAdministrationErrors.EmailAlreadyRegistered,
                TrainerAdministrationStoreStatus.ActorInvalid =>
                    TrainerAdministrationErrors.AdministratorOnly,
                TrainerAdministrationStoreStatus.ConcurrencyConflict =>
                    TrainerAdministrationErrors.ConcurrencyConflict,
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            });
        }

        var delivery = await _emailSender.SendTrainerInvitationAsync(
            new IssuedAuthenticationSecret(
                issued.Invitation.Email,
                generated.RawToken,
                issued.Invitation.ExpiresAt),
            cancellationToken);

        return delivery == AuthenticationEmailDeliveryOutcome.Sent
            ? Result<PendingTrainerInvitationDto>.Success(issued.Invitation)
            : Result<PendingTrainerInvitationDto>.Failure(AuthenticationErrors.EmailDeliveryUnavailable);
    }
}
