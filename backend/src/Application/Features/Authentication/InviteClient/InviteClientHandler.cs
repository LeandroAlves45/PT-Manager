using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.InviteClient;

/// <summary>Emite convite para uma ficha pertecente ao personal trainer autenticado.</summary>
public sealed class InviteClientHandler
{
    private readonly IValidator<InviteClientCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IClientInvitationStore _store;
    private readonly IAuthenticationEmailSender _emailSender;

    public InviteClientHandler(
        IValidator<InviteClientCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        AuthenticationPolicy policy,
        IClientInvitationStore store,
        IAuthenticationEmailSender emailSender)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    public async Task<Result> HandleAsync(
        InviteClientCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext,
            AuthenticationErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var outcome = await _store.IssueAsync(
            actor.Value.TrainerId,
            command.ClientId,
            command.Email.Trim(),
            now.Add(_policy.ClientInviteLifetime),
            now,
            cancellationToken
        );

        if (outcome.Kind != IssueClientInvitationStoreStatus.Issued)
        {
            return outcome.Kind switch
            {
                IssueClientInvitationStoreStatus.ClientNotFound =>
                    Result.Failure(AuthenticationErrors.ClientNotFound),
                IssueClientInvitationStoreStatus.ClientInactive =>
                    Result.Failure(AuthenticationErrors.ClientInactive),
                IssueClientInvitationStoreStatus.EmailMismatch =>
                    Result.Failure(AuthenticationErrors.InvitationEmailMismatch),
                IssueClientInvitationStoreStatus.RelationshipConflict =>
                    Result.Failure(AuthenticationErrors.RelationshipConflict),
                IssueClientInvitationStoreStatus.ConcurrencyConflict =>
                    Result.Failure(AuthenticationErrors.ConcurrencyConflict),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };
        }

        var delivery = await _emailSender.SendClientInvitationAsync(
            outcome.Secret!,
            cancellationToken
        );

        return delivery == AuthenticationEmailDeliveryOutcome.Sent
            ? Result.Success()
            : Result.Failure(AuthenticationErrors.EmailDeliveryUnavailable);
    }
}
