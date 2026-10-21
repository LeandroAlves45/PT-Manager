using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.InviteClient;

/// <summary>
/// Emite convite para uma ficha pertencente ao personal trainer autenticado, envia o email e
/// devolve o link e a validade para o trainer o poder copiar (U61). O link só existe aqui: a base
/// de dados guarda o hash do token.
/// </summary>
public sealed class InviteClientHandler
{
    private readonly IValidator<InviteClientCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IClientInvitationStore _store;
    private readonly IAuthenticationEmailSender _emailSender;
    private readonly IAuthenticationLinkFactory _links;

    public InviteClientHandler(
        IValidator<InviteClientCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        AuthenticationPolicy policy,
        IClientInvitationStore store,
        IAuthenticationEmailSender emailSender,
        IAuthenticationLinkFactory links)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _links = links ?? throw new ArgumentNullException(nameof(links));
    }

    public async Task<Result<InviteClientResultDto>> HandleAsync(
        InviteClientCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<InviteClientResultDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext,
            AuthenticationErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<InviteClientResultDto>.Failure(actor.Error!);

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
                    Result<InviteClientResultDto>.Failure(AuthenticationErrors.ClientNotFound),
                IssueClientInvitationStoreStatus.ClientInactive =>
                    Result<InviteClientResultDto>.Failure(AuthenticationErrors.ClientInactive),
                IssueClientInvitationStoreStatus.EmailMismatch =>
                    Result<InviteClientResultDto>.Failure(AuthenticationErrors.InvitationEmailMismatch),
                IssueClientInvitationStoreStatus.RelationshipConflict =>
                    Result<InviteClientResultDto>.Failure(AuthenticationErrors.RelationshipConflict),
                IssueClientInvitationStoreStatus.ConcurrencyConflict =>
                    Result<InviteClientResultDto>.Failure(AuthenticationErrors.ConcurrencyConflict),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };
        }

        var delivery = await _emailSender.SendClientInvitationAsync(
            outcome.Secret!,
            cancellationToken
        );

        // O convite já está gravado (e o anterior anulado) quando o email é tentado. Se o email
        // falhar, devolver o link mantém a ficha coerente: o convite existe, serve, e o personal
        // trainer copia o link e envia-o por outro canal.
        var secret = outcome.Secret!;
        return Result<InviteClientResultDto>.Success(new InviteClientResultDto(
            _links.ClientInvitation(secret.RawToken),
            secret.ExpiresAt,
            delivery == AuthenticationEmailDeliveryOutcome.Sent));
    }
}
