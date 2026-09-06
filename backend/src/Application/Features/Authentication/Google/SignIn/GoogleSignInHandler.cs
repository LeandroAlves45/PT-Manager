using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Features.Authentication.Google.Abstractions;
using Application.Features.Authentication.Google.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Identity;
using FluentValidation;

namespace Application.Features.Authentication.Google.SignIn;

/// <summary>Valida a credencial Google, orquestra o store e emite a sessão PT Manager.</summary>
public sealed class GoogleSignInHandler
{
    private readonly IValidator<GoogleSignInCommand> _validator;
    private readonly IExternalIdentityVerifier _verifier;
    private readonly IExternalAuthenticationStore _store;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly IAuthenticationEmailSender _emails;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;

    public GoogleSignInHandler(
        IValidator<GoogleSignInCommand> validator,
        IExternalIdentityVerifier verifier,
        IExternalAuthenticationStore store,
        IAccessTokenIssuer accessTokens,
        IAuthenticationEmailSender emails,
        IClock clock,
        AuthenticationPolicy policy)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _accessTokens = accessTokens ?? throw new ArgumentNullException(nameof(accessTokens));
        _emails = emails ?? throw new ArgumentNullException(nameof(emails));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async Task<Result<GoogleSignInOutcomeDto>> HandleAsync(
        GoogleSignInCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<GoogleSignInOutcomeDto>.Failure(validation.ToApplicationError());

        var verified = await _verifier.VerifyAsync(
            ExternalIdentity.GoogleProvider,
            command.IdToken,
            command.RawNonce,
            cancellationToken);
        if (!verified.IsSuccess)
            return Result<GoogleSignInOutcomeDto>.Failure(verified.Error!);

        var now = _clock.UtcNow;
        var stored = await _store.SignInAsync(
            verified.Value,
            command.RawNonce,
            string.IsNullOrWhiteSpace(command.InvitationToken) ? null : command.InvitationToken,
            now.AddDays(_policy.TrialDays),
            now.Add(_policy.EmailConfirmationLifetime),
            now.Add(_policy.RefreshSessionLifetime),
            now,
            cancellationToken);

        if (stored.Kind == GoogleSignInStoreStatus.Authenticated)
        {
            if (stored.Principal is null || stored.RefreshSession is null)
                throw new InvalidOperationException("Authenticated Google result is incomplete.");

            var access = _accessTokens.Issue(stored.Principal);
            return Result<GoogleSignInOutcomeDto>.Success(
                GoogleSignInOutcomeDto.Authenticated(AuthenticationSessionDto.Create(
                    stored.Principal,
                    access,
                    stored.RefreshSession)));
        }

        if (stored.Kind == GoogleSignInStoreStatus.EmailConfirmationRequired)
        {
            if (stored.EmailConfirmation is null)
                throw new InvalidOperationException("Pending Google result has no confirmation.");

            var delivery = await _emails.SendEmailConfirmationAsync(
                stored.EmailConfirmation,
                cancellationToken);

            return delivery == AuthenticationEmailDeliveryOutcome.Unavailable
                ? Result<GoogleSignInOutcomeDto>.Failure(AuthenticationErrors.EmailDeliveryUnavailable)
                : Result<GoogleSignInOutcomeDto>.Success(
                    GoogleSignInOutcomeDto.ConfirmationRequired());
        }

        var error = stored.Kind switch
        {
            GoogleSignInStoreStatus.ChallengeInvalid => GoogleAuthenticationErrors.InvalidCredential,
            GoogleSignInStoreStatus.AccountLinkRequired => GoogleAuthenticationErrors.AccountLinkRequired,
            GoogleSignInStoreStatus.AccountInactive => AuthenticationErrors.AccountInactive,
            GoogleSignInStoreStatus.RelationshipInactive => AuthenticationErrors.RelationshipConflict,
            GoogleSignInStoreStatus.InvitationInvalid => AuthenticationErrors.InvitationInvalid,
            GoogleSignInStoreStatus.InvitationExpired => AuthenticationErrors.InvitationExpired,
            GoogleSignInStoreStatus.InvitationConsumed => AuthenticationErrors.InvitationConsumed,
            GoogleSignInStoreStatus.InvitationEmailMismatch => AuthenticationErrors.InvitationEmailMismatch,
            GoogleSignInStoreStatus.RelationshipConflict => AuthenticationErrors.RelationshipConflict,
            GoogleSignInStoreStatus.ConcurrencyConflict => AuthenticationErrors.ConcurrencyConflict,
            _ => throw new ArgumentOutOfRangeException(nameof(stored.Kind))
        };
        return Result<GoogleSignInOutcomeDto>.Failure(error);
    }
}
