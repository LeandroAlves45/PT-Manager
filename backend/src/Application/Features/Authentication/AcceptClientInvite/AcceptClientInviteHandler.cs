using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.AcceptClientInvite;

/// <summary>Aceita um convite sem criar uma conta ou receber uma password.</summary>
public sealed class AcceptClientInviteHandler
{
    private readonly IValidator<AcceptClientInviteCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IClientInvitationStore _store;
    private readonly IAccessTokenIssuer _accessTokenIssuer;

    public AcceptClientInviteHandler(
        IValidator<AcceptClientInviteCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        AuthenticationPolicy policy,
        IClientInvitationStore store,
        IAccessTokenIssuer accessTokenIssuer)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
    }

    public async Task<Result<AuthenticationSessionDto>> HandleAsync(
        AcceptClientInviteCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<AuthenticationSessionDto>.Failure(validation.ToApplicationError());

        if (!_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty)
            return Result<AuthenticationSessionDto>.Failure(CommonErrors.UnauthenticatedUser);

        if (!string.Equals(
            _tenantContext.Role,
            "client",
            StringComparison.Ordinal))
            return Result<AuthenticationSessionDto>.Failure(AuthenticationErrors.ClientOnly);

        var now = _clock.UtcNow;
        var outcome = await _store.ConsumeAsync(
            command.Token,
            _tenantContext.UserId.Value,
            command.TransferApproved,
            now.Add(_policy.RefreshSessionLifetime),
            now,
            cancellationToken
        );

        if (outcome.Kind != ConsumeClientInvitationStoreStatus.Accepted)
        {
            var error = outcome.Kind switch
            {
                ConsumeClientInvitationStoreStatus.TokenNotFound =>
                    AuthenticationErrors.InvitationInvalid,
                ConsumeClientInvitationStoreStatus.TokenExpired =>
                    AuthenticationErrors.InvitationExpired,
                ConsumeClientInvitationStoreStatus.TokenAlreadyConsumed =>
                    AuthenticationErrors.InvitationConsumed,
                ConsumeClientInvitationStoreStatus.EmailMismatch =>
                    AuthenticationErrors.InvitationEmailMismatch,
                ConsumeClientInvitationStoreStatus.TransferApprovalRequired =>
                    AuthenticationErrors.TransferApprovalRequired,
                ConsumeClientInvitationStoreStatus.RelationshipConflict =>
                    AuthenticationErrors.RelationshipConflict,
                ConsumeClientInvitationStoreStatus.AccountInactive =>
                    AuthenticationErrors.AccountInactive,
                ConsumeClientInvitationStoreStatus.ConcurrencyConflict =>
                    AuthenticationErrors.ConcurrencyConflict,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };

            return Result<AuthenticationSessionDto>.Failure(error);
        }

        if (outcome.Principal is null || outcome.RefreshSession is null)
            throw new InvalidOperationException(
                "The client invitation store returned Accepted without a principal or refresh session.");

        var accessToken = _accessTokenIssuer.Issue(outcome.Principal);
        return Result<AuthenticationSessionDto>.Success(
            AuthenticationSessionDto.Create(
                outcome.Principal,
                accessToken,
                outcome.RefreshSession));
    }
}
