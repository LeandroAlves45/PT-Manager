using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Google.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.Entities.TrainerSettings;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Identity;

/// <summary>Persiste challenges, onboarding, sessão e linking Google de forma atómica.</summary>
internal sealed class ExternalAuthenticationStore :
    IExternalChallengeStore,
    IExternalAuthenticationStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly IOpaqueTokenService _tokens;
    private readonly UserManager<User> _userManager;
    private readonly ITenantContextInitializer _tenantInitializer;

    public ExternalAuthenticationStore(
        PtManagerDbContext dbContext,
        IOpaqueTokenService tokens,
        UserManager<User> userManager,
        ITenantContextInitializer tenantInitializer
    )
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tenantInitializer = tenantInitializer ?? throw new ArgumentNullException(nameof(tenantInitializer));
    }

    public async Task<IssuedExternalChallenge> IssueAsync(
        string purpose,
        Guid? userId,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var generated = _tokens.Generate();

        // A limpeza oportunista impede o crescimento ilimitado sem criar um job apenas
        // para registos efémeros; não participa em decisões de autorização.
        await _dbContext.Set<ExternalAuthenticationChallenge>()
            .Where(challenge => challenge.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.Set<ExternalAuthenticationChallenge>().Add(
            new ExternalAuthenticationChallenge(
                generated.TokenHash,
                purpose,
                userId,
                expiresAt,
                now
            )
        );
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedExternalChallenge(generated.RawToken, expiresAt);
    }

    public async Task<GoogleSignInStoreResult> SignInAsync(
        VerifiedExternalIdentity identity,
        string rawNonce,
        string? rawInvitationToken,
        DateTime trialEndsAt,
        DateTime confirmationExpiresAt,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var refresh = _tokens.Generate();
        var csrf = _tokens.Generate();
        var confirmation = _tokens.Generate();
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var challenge = await LockChallengeAsync(
                rawNonce,
                ExternalAuthenticationChallenge.SignInPurpose,
                null,
                now,
                cancellationToken);
            if (challenge is null)
                return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.ChallengeInvalid);

            // O challenge passa a consumido dentro da mesma transação. Em resultados
            // esperados sem mutação adicional, a transação confirma apenas esta remoção.
            _dbContext.Set<ExternalAuthenticationChallenge>().Remove(challenge);

            var external = await _dbContext.Set<ExternalIdentity>()
                .SingleOrDefaultAsync(candidate =>
                    candidate.Provider == identity.Provider &&
                    candidate.Subject == identity.Subject,
                    cancellationToken);

            if (external is not null)
            {
                GoogleSignInStoreResult returning;
                try
                {
                    returning = await SignInReturningAsync(
                        external,
                        challenge,
                        confirmation,
                        confirmationExpiresAt,
                        refresh,
                        csrf,
                        refreshExpiresAt,
                        now,
                        cancellationToken);
                }
                catch (ExternalAuthenticationConcurrencyException)
                {
                    return GoogleSignInStoreResult.Failure(
                        GoogleSignInStoreStatus.ConcurrencyConflict);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return returning;
            }

            var normalizedEmail = new EmailAddress(identity.Email).Normalized;
            if (await _dbContext.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken))
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.AccountLinkRequired);
            }

            GoogleSignInStoreResult created;
            try
            {
                created = rawInvitationToken is null
                    ? await CreateTrainerAsync(
                        identity,
                        challenge,
                        trialEndsAt,
                        confirmationExpiresAt,
                        refreshExpiresAt,
                        refresh,
                        csrf,
                        confirmation,
                        now,
                        cancellationToken)
                    : await CreateClientAsync(
                        identity,
                        challenge,
                        rawInvitationToken,
                        refreshExpiresAt,
                        refresh,
                        csrf,
                        now,
                        cancellationToken);
            }
            catch (ExternalAuthenticationConcurrencyException)
            {
                return GoogleSignInStoreResult.Failure(
                    GoogleSignInStoreStatus.ConcurrencyConflict);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        });
    }

    public async Task<GoogleLinkStoreStatus> LinkAsync(
        Guid userId,
        VerifiedExternalIdentity identity,
        string rawNonce,
        string currentPassword,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var challenge = await LockChallengeAsync(
                rawNonce,
                ExternalAuthenticationChallenge.LinkPurpose,
                userId,
                now,
                cancellationToken);
            if (challenge is null)
                return GoogleLinkStoreStatus.ChallengeInvalid;

            _dbContext.Set<ExternalAuthenticationChallenge>().Remove(challenge);

            var user = await _dbContext.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (user is null || !user.IsActive || user.IsDeleted)
                return await CompleteLinkFailureAsync(
                    GoogleLinkStoreStatus.UserNotFound,
                    transaction,
                    cancellationToken);

            if (!await _userManager.CheckPasswordAsync(user, currentPassword))
                return await CompleteLinkFailureAsync(
                    GoogleLinkStoreStatus.PasswordInvalid,
                    transaction,
                    cancellationToken);

            if (!string.Equals(
                user.NormalizedEmail,
                new EmailAddress(identity.Email).Normalized,
                StringComparison.Ordinal))
                return await CompleteLinkFailureAsync(
                    GoogleLinkStoreStatus.EmailMismatch,
                    transaction,
                    cancellationToken);

            var identities = _dbContext.Set<ExternalIdentity>();
            if (await identities.AnyAsync(candidate =>
                candidate.Provider == identity.Provider &&
                (candidate.Subject == identity.Subject || candidate.UserId == userId),
                cancellationToken))
                return await CompleteLinkFailureAsync(
                    GoogleLinkStoreStatus.IdentityConflict,
                    transaction,
                    cancellationToken);

            identities.Add(new ExternalIdentity(
                userId,
                identity.Provider,
                identity.Subject,
                now));
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return GoogleLinkStoreStatus.Linked;
            }
            catch (DbUpdateConcurrencyException)
            {
                return GoogleLinkStoreStatus.ConcurrencyConflict;
            }
            catch (DbUpdateException exception) when (IsExternalIdentityConflict(exception))
            {
                return GoogleLinkStoreStatus.IdentityConflict;
            }
        });
    }

    private async Task<GoogleSignInStoreResult> SignInReturningAsync(
        ExternalIdentity external,
        ExternalAuthenticationChallenge challenge,
        GeneratedOpaqueToken confirmation,
        DateTime confirmationExpiresAt,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {external.UserId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null || !user.IsActive || user.IsDeleted)
            return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.AccountInactive);

        if (!user.EmailConfirmed)
        {
            var previous = await _dbContext.EmailVerificationTokens
                .Where(token => token.UserId == user.Id && token.ConsumedAt == null)
                .ToListAsync(cancellationToken);
            _dbContext.EmailVerificationTokens.RemoveRange(previous);
            _dbContext.EmailVerificationTokens.Add(new EmailVerificationToken(
                user.Id,
                confirmation.TokenHash,
                confirmationExpiresAt,
                now));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return GoogleSignInStoreResult.ConfirmationRequired(
                new IssuedAuthenticationSecret(
                    user.Email,
                    confirmation.RawToken,
                    confirmationExpiresAt
                ));
        }

        var principal = await ResolvePrincipalAsync(user, cancellationToken);
        if (principal is null)
            return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.RelationshipInactive);

        AddRefresh(user.Id, refresh, csrf, refreshExpiresAt, now);
        _dbContext.Set<ExternalAuthenticationChallenge>().Remove(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);
        Establish(principal);
        return GoogleSignInStoreResult.Authenticated(
            principal,
            new IssuedRefreshSession(refresh.RawToken, csrf.RawToken, refreshExpiresAt));
    }

    private async Task<GoogleSignInStoreResult> CreateTrainerAsync(
        VerifiedExternalIdentity identity,
        ExternalAuthenticationChallenge challenge,
        DateTime trialEndsAt,
        DateTime confirmationExpiresAt,
        DateTime refreshExpiresAt,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        GeneratedOpaqueToken confirmation,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var user = new User(
            new EmailAddress(identity.Email),
            "trainer",
            identity.FullName,
            now);
        if (identity.IsEmailAuthoritative)
            user.ConfirmEmail(now);

        _dbContext.Users.Add(user);
        _dbContext.Set<ExternalIdentity>().Add(new ExternalIdentity(
            user.Id,
            identity.Provider,
            identity.Subject,
            now));
        _dbContext.TrainerSettings.Add(new TrainerSettings(user.Id, now));
        _dbContext.TrainerSubscriptions.Add(new TrainerSubscription(user.Id, trialEndsAt, now));

        // TrainerSettings e TrainerSubscription são política A': o interceptor de tenant
        // exige um tenant efetivo no SaveChanges. O trainer é a raiz do seu próprio
        // tenant, pelo que este é o instante em que passa a existir — estabelecer só
        // depois de gravar faria toda a criação de conta Google falhar.
        var principal = new AuthenticatedPrincipal(
            user.Id,
            user.Id,
            user.Role,
            user.SecurityStamp);
        Establish(principal);

        if (!identity.IsEmailAuthoritative)
        {
            _dbContext.EmailVerificationTokens.Add(new EmailVerificationToken(
                user.Id,
                confirmation.TokenHash,
                confirmationExpiresAt,
                now));

            await SaveSignInAsync(cancellationToken);
            return GoogleSignInStoreResult.ConfirmationRequired(
                new IssuedAuthenticationSecret(
                    user.Email,
                    confirmation.RawToken,
                    confirmationExpiresAt
                ));
        }

        AddRefresh(user.Id, refresh, csrf, refreshExpiresAt, now);
        await SaveSignInAsync(cancellationToken);
        return GoogleSignInStoreResult.Authenticated(
            principal,
            new IssuedRefreshSession(refresh.RawToken, csrf.RawToken, refreshExpiresAt));
    }

    private async Task<GoogleSignInStoreResult> CreateClientAsync(
        VerifiedExternalIdentity identity,
        ExternalAuthenticationChallenge challenge,
        string rawInvitationToken,
        DateTime refreshExpiresAt,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var invitationHash = _tokens.Hash(rawInvitationToken);
        var invitation = await _dbContext.InviteTokens
            .FromSqlInterpolated($"SELECT * FROM invite_tokens WHERE token_hash = {invitationHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (invitation is null)
            return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.InvitationInvalid);
        if (invitation.UsedAt.HasValue)
            return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.InvitationConsumed);
        if (invitation.ExpiresAt <= now)
            return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.InvitationExpired);

        if (!string.Equals(
            new EmailAddress(invitation.Email).Normalized,
            new EmailAddress(identity.Email).Normalized,
            StringComparison.Ordinal))
            return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.InvitationEmailMismatch);

        var client = await _dbContext.Clients
            .FromSqlInterpolated($"SELECT * FROM clients WHERE owner_trainer_id = {invitation.TrainerId} AND id = {invitation.ClientId} FOR UPDATE")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
        if (client is null || client.IsDeleted || !client.IsActive || client.UserId.HasValue)
            return GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.RelationshipConflict);

        var user = new User(
            new EmailAddress(identity.Email),
            "client",
            identity.FullName,
            now);
        user.ConfirmEmail(now);
        client.AttachUser(user.Id, now);
        invitation.MarkUsed(now);

        _dbContext.Users.Add(user);
        _dbContext.Set<ExternalIdentity>().Add(new ExternalIdentity(
            user.Id,
            identity.Provider,
            identity.Subject,
            now));
        AddRefresh(user.Id, refresh, csrf, refreshExpiresAt, now);

        // O Client alterado é política A: o interceptor valida a ownership contra o
        // tenant efetivo, que aqui é o trainer dono do convite. Tem de estar
        // estabelecido antes do SaveChanges, não depois.
        var principal = new AuthenticatedPrincipal(
            user.Id,
            invitation.TrainerId,
            user.Role,
            user.SecurityStamp);
        Establish(principal);
        await SaveSignInAsync(cancellationToken);

        return GoogleSignInStoreResult.Authenticated(
            principal,
            new IssuedRefreshSession(refresh.RawToken, csrf.RawToken, refreshExpiresAt));
    }

    private async Task<ExternalAuthenticationChallenge?> LockChallengeAsync(
        string rawNonce,
        string purpose,
        Guid? userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var nonceHash = _tokens.Hash(rawNonce);
        var challenge = await _dbContext.Set<ExternalAuthenticationChallenge>()
            .FromSqlInterpolated($"SELECT * FROM external_authentication_challenges WHERE nonce_hash = {nonceHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        return challenge is not null && !challenge.IsExpired(now) &&
            challenge.Purpose == purpose && challenge.UserId == userId
            ? challenge
            : null;
    }

    private async Task<GoogleLinkStoreStatus> CompleteLinkFailureAsync(
        GoogleLinkStoreStatus status,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return status;
    }

    private async Task<AuthenticatedPrincipal?> ResolvePrincipalAsync(
        User user,
        CancellationToken cancellationToken)
    {
        Guid? trainerId = user.Role switch
        {
            "trainer" => user.Id,
            "superuser" => null,
            "client" => await _dbContext.Clients
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(client => client.UserId == user.Id && client.IsActive && !client.IsDeleted)
                .Select(client => (Guid?)client.OwnerTrainerId)
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
        return user.Role != "superuser" && !trainerId.HasValue
            ? null
            : new AuthenticatedPrincipal(user.Id, trainerId, user.Role, user.SecurityStamp);
    }

    private void AddRefresh(
        Guid userId,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        DateTime expiresAt,
        DateTime now) =>
        _dbContext.RefreshTokens.Add(new RefreshToken(
            userId,
            Guid.NewGuid(),
            refresh.TokenHash,
            csrf.TokenHash,
            null,
            expiresAt,
            now));

    private async Task SaveSignInAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ExternalAuthenticationConcurrencyException();
        }
        catch (DbUpdateException exception) when (
            IsExternalIdentityConflict(exception) || IsDuplicateEmail(exception))
        {
            throw new ExternalAuthenticationConcurrencyException();
        }
    }

    private void Establish(AuthenticatedPrincipal principal) =>
        _tenantInitializer.Establish(
            principal.TrainerId,
            principal.UserId,
            principal.Role,
            TenantOrigin.System,
            false);

    private static bool IsExternalIdentityConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
        postgres.ConstraintName is "uq_external_identities_provider_subject" or
            "uq_external_identities_user_provider";

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
        postgres.ConstraintName == "uq_users_normalized_email";

    private sealed class ExternalAuthenticationConcurrencyException : Exception;
}
