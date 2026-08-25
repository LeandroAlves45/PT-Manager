using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Authentication.Abstractions;
using Application.Results;

namespace Application.Features.Authentication.ResendEmailConfirmation;

/// <summary>Reenvia a confirmação para a conta autenticada.</summary>
public sealed class ResendEmailConfirmationHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IEmailConfirmationStore _store;
    private readonly IAuthenticationEmailSender _emailSender;

    public ResendEmailConfirmationHandler(
        ITenantContext tenantContext,
        IClock clock,
        AuthenticationPolicy policy,
        IEmailConfirmationStore store,
        IAuthenticationEmailSender emailSender)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    public async Task<Result> HandleAsync(
        CancellationToken cancellationToken
    )
    {
        if (!_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty)
            return Result.Failure(CommonErrors.UnauthenticatedUser);

        var now = _clock.UtcNow;
        var outcome = await _store.IssueAsync(
            _tenantContext.UserId.Value,
            now.Add(_policy.EmailConfirmationLifetime),
            now,
            cancellationToken
        );

        if (outcome.Kind != EmailConfirmationStoreStatus.Issued)
            return outcome.Kind switch
            {
                EmailConfirmationStoreStatus.AlreadyConfirmed =>
                    Result.Failure(AuthenticationErrors.EmailAlreadyConfirmed),
                EmailConfirmationStoreStatus.AccountInactive =>
                    Result.Failure(AuthenticationErrors.AccountInactive),
                EmailConfirmationStoreStatus.UserNotFound =>
                    Result.Failure(CommonErrors.UnauthenticatedUser),
                EmailConfirmationStoreStatus.ConcurrencyConflict =>
                    Result.Failure(AuthenticationErrors.ConcurrencyConflict),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };

        if (outcome.Secret is null)
            throw new InvalidOperationException(
                "The email confirmation store returned Issued without a secret.");

        var delivery = await _emailSender.SendEmailConfirmationAsync(
            outcome.Secret,
            cancellationToken
        );

        return delivery == AuthenticationEmailDeliveryOutcome.Sent
            ? Result.Success()
            : Result.Failure(AuthenticationErrors.EmailDeliveryUnavailable);
    }
}
