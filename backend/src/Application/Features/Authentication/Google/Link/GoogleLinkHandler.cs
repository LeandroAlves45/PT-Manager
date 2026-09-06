using Application.Common.Abstractions;
using Application.Features.Authentication.Google.Abstractions;
using Application.Results;
using Application.Validation;
using Domain.Entities.Identity;
using FluentValidation;

namespace Application.Features.Authentication.Google.Link;

/// <summary>Liga Google ao utilizador autenticado após verificar credencial e password.</summary>
public sealed class GoogleLinkHandler
{
    private readonly IValidator<GoogleLinkCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IExternalIdentityVerifier _verifier;
    private readonly IExternalAuthenticationStore _store;
    private readonly IClock _clock;

    public GoogleLinkHandler(
        IValidator<GoogleLinkCommand> validator,
        ITenantContext tenantContext,
        IExternalIdentityVerifier verifier,
        IExternalAuthenticationStore store,
        IClock clock)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> HandleAsync(
        GoogleLinkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        if (!_tenantContext.UserId.HasValue || _tenantContext.UserId == Guid.Empty ||
            _tenantContext.Role is not ("trainer" or "client" or "superuser"))
            return Result.Failure(AuthenticationErrors.AuthenticatedAccountRequired);

        var verified = await _verifier.VerifyAsync(
            ExternalIdentity.GoogleProvider,
            command.IdToken,
            command.RawNonce,
            cancellationToken);
        if (!verified.IsSuccess)
            return Result.Failure(verified.Error!);

        var status = await _store.LinkAsync(
            _tenantContext.UserId.Value,
            verified.Value,
            command.RawNonce,
            command.CurrentPassword,
            _clock.UtcNow,
            cancellationToken);

        return status switch
        {
            GoogleLinkStoreStatus.Linked => Result.Success(),
            GoogleLinkStoreStatus.ChallengeInvalid =>
                Result.Failure(GoogleAuthenticationErrors.InvalidCredential),
            GoogleLinkStoreStatus.UserNotFound =>
                Result.Failure(AuthenticationErrors.AuthenticatedAccountRequired),
            GoogleLinkStoreStatus.PasswordInvalid =>
                Result.Failure(AuthenticationErrors.CurrentPasswordInvalid),
            GoogleLinkStoreStatus.EmailMismatch =>
                Result.Failure(GoogleAuthenticationErrors.LinkingEmailMismatch),
            GoogleLinkStoreStatus.IdentityConflict =>
                Result.Failure(GoogleAuthenticationErrors.IdentityConflict),
            GoogleLinkStoreStatus.ConcurrencyConflict =>
                Result.Failure(AuthenticationErrors.ConcurrencyConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
}
