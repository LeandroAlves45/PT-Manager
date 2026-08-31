using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using FluentValidation;

namespace Application.UnitTests.Features.Authentication;

internal sealed class ValidValidator<T> : AbstractValidator<T> { }

internal sealed class TestClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}

internal sealed class TestTenantContext : ITenantContext
{
    public Guid? TrainerId { get; init; }
    public Guid? UserId { get; init; }
    public string? Role { get; init; }
    public TenantOrigin Origin { get; init; } = TenantOrigin.Http;
    public bool IsAdministrative { get; init; }
}

internal sealed class TestEmailSender : IAuthenticationEmailSender
{
    public AuthenticationEmailDeliveryOutcome Outcome { get; set; } =
        AuthenticationEmailDeliveryOutcome.Sent;
    public int ConfirmationCalls { get; private set; }
    public int InvitationCalls { get; private set; }
    public int ResetCalls { get; private set; }

    public Task<AuthenticationEmailDeliveryOutcome> SendEmailConfirmationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken)
    {
        ConfirmationCalls++;
        return Task.FromResult(Outcome);
    }

    public Task<AuthenticationEmailDeliveryOutcome> SendClientInvitationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken)
    {
        InvitationCalls++;
        return Task.FromResult(Outcome);
    }

    public Task<AuthenticationEmailDeliveryOutcome> SendPasswordResetAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken)
    {
        ResetCalls++;
        return Task.FromResult(Outcome);
    }
}

internal sealed class RegistrationStoreStub(RegisterTrainerStoreResult result) :
    IAuthenticationRegistrationStore
{
    public int Calls { get; private set; }

    public Task<RegisterTrainerStoreResult> RegisterTrainerAsync(
        RegisterTrainerStoreRequest request,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(result);
    }
}

internal sealed class EmailConfirmationStoreStub(EmailConfirmationStoreResult result) :
    IEmailConfirmationStore
{
    public Task<EmailConfirmationStoreResult> IssueAsync(
        Guid userId,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken) => Task.FromResult(result);

    public Task<EmailConfirmationStoreResult> ConsumeAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken) => Task.FromResult(result);
}

internal sealed class PasswordResetRequestStoreStub(PasswordResetRequestStoreResult result) :
    IPasswordResetRequestStore
{
    public Task<PasswordResetRequestStoreResult> IssueAsync(
        string email,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken) => Task.FromResult(result);
}

internal sealed class ClientInvitationStoreStub : IClientInvitationStore
{
    public IssueClientInvitationStoreResult IssueResult { get; set; } =
        IssueClientInvitationStoreResult.For(IssueClientInvitationStoreStatus.ClientNotFound);
    public ConsumeClientInvitationStoreResult ConsumeResult { get; set; } =
        ConsumeClientInvitationStoreResult.For(ConsumeClientInvitationStoreStatus.TokenNotFound);
    public int IssueCalls { get; private set; }

    public Task<IssueClientInvitationStoreResult> IssueAsync(
        Guid trainerId,
        Guid clientId,
        string email,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        IssueCalls++;
        return Task.FromResult(IssueResult);
    }

    public Task<ConsumeClientInvitationStoreResult> ConsumeAsync(
        string rawToken,
        Guid authenticatedUserId,
        bool transferApproved,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken) => Task.FromResult(ConsumeResult);
}

internal sealed class SessionStoreStub : IAuthenticationSessionStore
{
    public AuthenticateStoreResult AuthenticateResult { get; set; } =
        AuthenticateStoreResult.Failure(AuthenticateStoreStatus.InvalidCredentials);
    public RotateRefreshStoreResult RotateResult { get; set; } =
        RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.NotFound);
    public int RevokeCalls { get; private set; }

    public Task<AuthenticateStoreResult> AuthenticateAsync(
        string email,
        string password,
        DateTime now,
        DateTime refreshExpiresAt,
        CancellationToken cancellationToken) => Task.FromResult(AuthenticateResult);

    public RotateCsrfStoreResult RotateCsrfResult { get; set; } =
        RotateCsrfStoreResult.Failure(RotateCsrfStoreStatus.NotFound);
    public RevokeSessionStoreStatus RevokeResult { get; set; } =
        RevokeSessionStoreStatus.NotFound;

    /// <summary>Último segredo anti-CSRF apresentado ao store, para asserção.</summary>
    public string? LastPresentedCsrfToken { get; private set; }

    public Task<RotateRefreshStoreResult> RotateAsync(
        string rawToken,
        string rawCsrfToken,
        DateTime now,
        DateTime refreshExpiresAt,
        CancellationToken cancellationToken)
    {
        LastPresentedCsrfToken = rawCsrfToken;
        return Task.FromResult(RotateResult);
    }

    public Task<RotateCsrfStoreResult> RotateCsrfAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken) => Task.FromResult(RotateCsrfResult);

    public Task<RevokeSessionStoreStatus> RevokeAsync(
        string rawToken,
        string rawCsrfToken,
        DateTime now,
        CancellationToken cancellationToken)
    {
        RevokeCalls++;
        LastPresentedCsrfToken = rawCsrfToken;
        return Task.FromResult(RevokeResult);
    }

    public Task RevokeAllAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class PasswordStoreStub(PasswordManagementStoreResult result) :
    IPasswordManagementStore
{
    public int ChangeCalls { get; private set; }

    public Task<PasswordManagementStoreResult> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ChangeCalls++;
        return Task.FromResult(result);
    }

    public Task<PasswordManagementStoreResult> ResetAsync(
        string rawToken,
        string newPassword,
        DateTime now,
        CancellationToken cancellationToken) => Task.FromResult(result);
}

internal sealed class AccessTokenIssuerStub : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(AuthenticatedPrincipal principal) =>
        new("access-token", new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc));
}
