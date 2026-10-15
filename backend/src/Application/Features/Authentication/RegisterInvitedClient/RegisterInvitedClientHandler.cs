using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.RegisterInvitedClient;

/// <summary>Cria a conta de um cliente convidado e abre a sessão no mesmo passo.</summary>
public sealed class RegisterInvitedClientHandler
{
    private readonly IValidator<RegisterInvitedClientCommand> _validator;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IClientInvitationStore _store;
    private readonly IAccessTokenIssuer _accessTokenIssuer;

    /// <summary>Cria o handler com as dependências do registo por convite.</summary>
    public RegisterInvitedClientHandler(
        IValidator<RegisterInvitedClientCommand> validator,
        IClock clock,
        AuthenticationPolicy policy,
        IClientInvitationStore store,
        IAccessTokenIssuer accessTokenIssuer)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
    }

    /// <summary>Valida o pedido, regista o cliente e devolve a sessão aberta.</summary>
    public async Task<Result<AuthenticationSessionDto>> HandleAsync(
        RegisterInvitedClientCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<AuthenticationSessionDto>.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var outcome = await _store.RegisterClientAsync(
            command.Token,
            command.Password,
            now.Add(_policy.RefreshSessionLifetime),
            now,
            cancellationToken);

        if (outcome.Kind != RegisterInvitedClientStoreStatus.Registered)
        {
            var error = outcome.Kind switch
            {
                RegisterInvitedClientStoreStatus.TokenNotFound =>
                    AuthenticationErrors.InvitationInvalid,
                RegisterInvitedClientStoreStatus.TokenExpired =>
                    AuthenticationErrors.InvitationExpired,
                RegisterInvitedClientStoreStatus.TokenAlreadyConsumed =>
                    AuthenticationErrors.InvitationConsumed,
                RegisterInvitedClientStoreStatus.AccountAlreadyExists =>
                    AuthenticationErrors.InvitationAccountExists,
                RegisterInvitedClientStoreStatus.PasswordRejected =>
                    AuthenticationErrors.RegistrationRejected(),
                RegisterInvitedClientStoreStatus.RelationshipConflict =>
                    AuthenticationErrors.RelationshipConflict,
                RegisterInvitedClientStoreStatus.ConcurrencyConflict =>
                    AuthenticationErrors.ConcurrencyConflict,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };

            return Result<AuthenticationSessionDto>.Failure(error);
        }

        if (outcome.Principal is null || outcome.RefreshSession is null)
            throw new InvalidOperationException(
                "The client invitation store returned Registered without a principal or refresh token.");

        var accessToken = _accessTokenIssuer.Issue(outcome.Principal);
        return Result<AuthenticationSessionDto>.Success(
            AuthenticationSessionDto.Create(
                outcome.Principal,
                accessToken,
                outcome.RefreshSession));
    }
}
