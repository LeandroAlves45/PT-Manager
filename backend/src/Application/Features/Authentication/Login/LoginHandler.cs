using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.Login;

/// <summary>Orquestra o login sem conhecer Identity, EF Core ou JWT.</summary>
public sealed class LoginHandler
{
    private readonly IValidator<LoginCommand> _validator;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IAuthenticationSessionStore _store;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IAccountAccessQueries _accessQueries;

    public LoginHandler(
        IValidator<LoginCommand> validator,
        IClock clock,
        AuthenticationPolicy policy,
        IAuthenticationSessionStore store,
        IAccessTokenIssuer accessTokenIssuer,
        IAccountAccessQueries accessQueries)
    {
        _accessQueries = accessQueries ?? throw new ArgumentNullException(nameof(accessQueries));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
    }

    public async Task<Result<AuthenticationSessionDto>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<AuthenticationSessionDto>.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var outcome = await _store.AuthenticateAsync(
            command.Email.Trim(),
            command.Password,
            now,
            now.Add(_policy.RefreshSessionLifetime),
            cancellationToken
        );

        if (outcome.Kind != AuthenticateStoreStatus.Authenticated)
        {
            var error = outcome.Kind switch
            {
                AuthenticateStoreStatus.InvalidCredentials or
                AuthenticateStoreStatus.LockedOut or
                AuthenticateStoreStatus.EmailNotConfirmed or
                AuthenticateStoreStatus.AccountInactive or
                AuthenticateStoreStatus.RelationshipInactive =>
                    AuthenticationErrors.InvalidCredentials,
                AuthenticateStoreStatus.ConcurrencyConflict =>
                    AuthenticationErrors.ConcurrencyConflict,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };

            return Result<AuthenticationSessionDto>.Failure(error);
        }

        if (outcome.Principal is null || outcome.RefreshSession is null)
            throw new InvalidOperationException(
                "The authentication session store returned Authenticated without a principal or refresh session.");

        // Bloqueio e fim do prazo só se revelam depois da palavra-passe certa.
        var blocked = await AccountAccessGate.CheckAsync(
            _accessQueries, _store, outcome.Principal, now, cancellationToken);
        if (blocked is not null)
            return Result<AuthenticationSessionDto>.Failure(blocked);

        var accessToken = _accessTokenIssuer.Issue(outcome.Principal);
        return Result<AuthenticationSessionDto>.Success(
            AuthenticationSessionDto.Create(
                outcome.Principal,
                accessToken,
                outcome.RefreshSession));
    }
}
