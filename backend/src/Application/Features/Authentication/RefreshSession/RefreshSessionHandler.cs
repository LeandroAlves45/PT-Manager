using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.RefreshSession;

/// <summary>Orquestra a rotation e emissão sem recuperar de reuso.</summary>
public sealed class RefreshSessionHandler
{
    private readonly IValidator<RefreshSessionCommand> _validator;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IAuthenticationSessionStore _store;
    private readonly IAccessTokenIssuer _accessTokenIssuer;

    public RefreshSessionHandler(
        IValidator<RefreshSessionCommand> validator,
        IClock clock,
        AuthenticationPolicy policy,
        IAuthenticationSessionStore store,
        IAccessTokenIssuer accessTokenIssuer)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
    }

    public async Task<Result<AuthenticationSessionDto>> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<AuthenticationSessionDto>.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var outcome = await _store.RotateAsync(
            command.RawToken,
            command.RawCsrfToken,
            now,
            now.Add(_policy.RefreshSessionLifetime),
            cancellationToken
        );

        if (outcome.Kind != RotateRefreshStoreStatus.Rotated)
        {
            var error = outcome.Kind switch
            {
                RotateRefreshStoreStatus.NotFound or
                RotateRefreshStoreStatus.Expired or
                RotateRefreshStoreStatus.Reused or
                RotateRefreshStoreStatus.PrincipalInvalid =>
                    AuthenticationErrors.RefreshSessionInvalid,
                RotateRefreshStoreStatus.CsrfInvalid =>
                    AuthenticationErrors.CsrfTokenInvalid,
                RotateRefreshStoreStatus.ConcurrencyConflict =>
                    AuthenticationErrors.ConcurrencyConflict,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };

            return Result<AuthenticationSessionDto>.Failure(error);
        }

        if (outcome.Principal is null || outcome.RefreshSession is null)
            throw new InvalidOperationException(
                "The refresh session store returned Rotated without a principal or refresh session.");

        var accessToken = _accessTokenIssuer.Issue(outcome.Principal);
        return Result<AuthenticationSessionDto>.Success(
            AuthenticationSessionDto.Create(
                outcome.Principal,
                accessToken,
                outcome.RefreshSession));
    }
}
