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

    /// <summary>Inicializa o store com contexto, tokens opacos, Identity e tenant.</summary>
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

    /// <summary>Emite um challenge efémero e remove expirados antes de persistir.</summary>
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

    /// <summary>
    /// Conclui o sign-in Google: utilizador existente, link necessário ou criação
    /// Personal Trainer ou Cliente.
    /// </summary>
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
        var prospectiveUser = new User(
            new EmailAddress(identity.Email),
            rawInvitationToken is null ? "trainer" : "client",
            identity.FullName,
            now);
        var prospectiveExternalIdentity = new ExternalIdentity(
            prospectiveUser.Id,
            identity.Provider,
            identity.Subject,
            now);
        var attemptState = new SignInAttemptState();
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;
            _dbContext.ChangeTracker.Clear();

            // Os hashes emitidos identificam de forma inequívoca esta invocação. Se o
            // commit anterior foi confirmado pelo PostgreSQL mas a confirmação não chegou
            // ao processo, devolvemos o mesmo resultado sem repetir a escrita.
            if (attempt > 1)
            {
                var completed = await TryResolveCompletedSignInAsync(
                    refresh,
                    csrf,
                    confirmation,
                    refreshExpiresAt,
                    confirmationExpiresAt,
                    attemptState,
                    cancellationToken);
                if (completed is not null)
                    return completed;
            }

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
                        attemptState,
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
                        prospectiveUser,
                        prospectiveExternalIdentity,
                        attemptState,
                        now,
                        cancellationToken)
                    : await CreateClientAsync(
                        identity,
                        challenge,
                        rawInvitationToken,
                        refreshExpiresAt,
                        refresh,
                        csrf,
                        prospectiveUser,
                        prospectiveExternalIdentity,
                        attemptState,
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

    /// <summary>Liga uma identidade Google a uma conta local após validar password e email.</summary>
    public async Task<GoogleLinkStoreStatus> LinkAsync(
        Guid userId,
        VerifiedExternalIdentity identity,
        string rawNonce,
        string currentPassword,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var linkedIdentity = new ExternalIdentity(
            userId,
            identity.Provider,
            identity.Subject,
            now);
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;
            _dbContext.ChangeTracker.Clear();

            if (attempt > 1)
            {
                var completed = await ResolveCompletedLinkAsync(
                    userId,
                    identity,
                    cancellationToken);
                if (completed.HasValue)
                    return completed.Value;
            }

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

            identities.Add(linkedIdentity);
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

    /// <summary>Autentica um utilizador com identidade externa já persistida.</summary>
    private async Task<GoogleSignInStoreResult> SignInReturningAsync(
        ExternalIdentity external,
        ExternalAuthenticationChallenge challenge,
        GeneratedOpaqueToken confirmation,
        DateTime confirmationExpiresAt,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        DateTime refreshExpiresAt,
        SignInAttemptState attemptState,
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
            attemptState.Confirmation ??= new EmailVerificationToken(
                user.Id, confirmation.TokenHash, confirmationExpiresAt, now);
            _dbContext.EmailVerificationTokens.Add(attemptState.Confirmation);
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

        attemptState.Refresh ??= CreateRefresh(user.Id, refresh, csrf, refreshExpiresAt, now);
        _dbContext.RefreshTokens.Add(attemptState.Refresh);
        _dbContext.Set<ExternalAuthenticationChallenge>().Remove(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);
        EstablishOnce(principal, attemptState);
        return GoogleSignInStoreResult.Authenticated(
            principal,
            new IssuedRefreshSession(refresh.RawToken, csrf.RawToken, refreshExpiresAt));
    }

    /// <summary>Cria conta do personal trainer, subscrição trial e identidade Google numa transação.</summary>
    private async Task<GoogleSignInStoreResult> CreateTrainerAsync(
        VerifiedExternalIdentity identity,
        ExternalAuthenticationChallenge challenge,
        DateTime trialEndsAt,
        DateTime confirmationExpiresAt,
        DateTime refreshExpiresAt,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        GeneratedOpaqueToken confirmation,
        User user,
        ExternalIdentity externalIdentity,
        SignInAttemptState attemptState,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (identity.IsEmailAuthoritative)
            user.ConfirmEmail(now);

        _dbContext.Users.Add(user);
        _dbContext.Set<ExternalIdentity>().Add(externalIdentity);
        attemptState.TrainerSettings ??= new TrainerSettings(user.Id, now);
        attemptState.Subscription ??= new TrainerSubscription(user.Id, trialEndsAt, now);
        _dbContext.TrainerSettings.Add(attemptState.TrainerSettings);
        _dbContext.TrainerSubscriptions.Add(attemptState.Subscription);

        // TrainerSettings e TrainerSubscription são política A': o interceptor de tenant
        // exige um tenant efetivo no SaveChanges. O pertrainer é a raiz do seu próprio
        // tenant, pelo que este é o instante em que passa a existir — estabelecer só
        // depois de gravar faria toda a criação de conta Google falhar.
        var principal = new AuthenticatedPrincipal(
            user.Id,
            user.Id,
            user.Role,
            user.SecurityStamp);
        EstablishOnce(principal, attemptState);

        if (!identity.IsEmailAuthoritative)
        {
            attemptState.Confirmation ??= new EmailVerificationToken(
                user.Id, confirmation.TokenHash, confirmationExpiresAt, now);
            _dbContext.EmailVerificationTokens.Add(attemptState.Confirmation);

            await SaveSignInAsync(cancellationToken);
            return GoogleSignInStoreResult.ConfirmationRequired(
                new IssuedAuthenticationSecret(
                    user.Email,
                    confirmation.RawToken,
                    confirmationExpiresAt
                ));
        }

        attemptState.Refresh ??= CreateRefresh(user.Id, refresh, csrf, refreshExpiresAt, now);
        _dbContext.RefreshTokens.Add(attemptState.Refresh);
        await SaveSignInAsync(cancellationToken);
        return GoogleSignInStoreResult.Authenticated(
            principal,
            new IssuedRefreshSession(refresh.RawToken, csrf.RawToken, refreshExpiresAt));
    }

    /// <summary>Consome convite, associa cliente e cria conta com identidade Google.</summary>
    private async Task<GoogleSignInStoreResult> CreateClientAsync(
        VerifiedExternalIdentity identity,
        ExternalAuthenticationChallenge challenge,
        string rawInvitationToken,
        DateTime refreshExpiresAt,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        User user,
        ExternalIdentity externalIdentity,
        SignInAttemptState attemptState,
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

        user.ConfirmEmail(now);
        client.AttachUser(user.Id, now);
        invitation.MarkUsed(now);

        _dbContext.Users.Add(user);
        _dbContext.Set<ExternalIdentity>().Add(externalIdentity);
        attemptState.Refresh ??= CreateRefresh(user.Id, refresh, csrf, refreshExpiresAt, now);
        _dbContext.RefreshTokens.Add(attemptState.Refresh);

        // O Client alterado é política A: o interceptor valida a ownership contra o
        // tenant efetivo, que aqui é o personal trainer dono do convite. Tem de estar
        // estabelecido antes do SaveChanges, não depois.
        var principal = new AuthenticatedPrincipal(
            user.Id,
            invitation.TrainerId,
            user.Role,
            user.SecurityStamp);
        EstablishOnce(principal, attemptState);
        await SaveSignInAsync(cancellationToken);

        return GoogleSignInStoreResult.Authenticated(
            principal,
            new IssuedRefreshSession(refresh.RawToken, csrf.RawToken, refreshExpiresAt));
    }

    /// <summary>Bloqueia e valida o challenge de nonce para o propósito e utilizador esperados.</summary>
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

    /// <summary>Confirma a remoção do challenge e devolve o estado de falha do link.</summary>
    private async Task<GoogleLinkStoreStatus> CompleteLinkFailureAsync(
        GoogleLinkStoreStatus status,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return status;
    }

    /// <summary>Resolve o principal autenticado e o tenant efetivo consoante o role.</summary>
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

    /// <summary>Reconstrói o resultado de um sign-in já commitado após retry da execution strategy.</summary>
    private async Task<GoogleSignInStoreResult?> TryResolveCompletedSignInAsync(
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        GeneratedOpaqueToken confirmation,
        DateTime refreshExpiresAt,
        DateTime confirmationExpiresAt,
        SignInAttemptState attemptState,
        CancellationToken cancellationToken)
    {
        var refreshedUserId = await _dbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == refresh.TokenHash &&
                token.CsrfTokenHash == csrf.TokenHash)
            .Select(token => (Guid?)token.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (refreshedUserId.HasValue)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == refreshedUserId.Value,
                    cancellationToken);
            if (user is null)
                return GoogleSignInStoreResult.Failure(
                    GoogleSignInStoreStatus.ConcurrencyConflict);

            var principal = await ResolvePrincipalAsync(user, cancellationToken);
            if (principal is null)
                return GoogleSignInStoreResult.Failure(
                    GoogleSignInStoreStatus.RelationshipInactive);

            EstablishOnce(principal, attemptState);
            return GoogleSignInStoreResult.Authenticated(
                principal,
                new IssuedRefreshSession(
                    refresh.RawToken,
                    csrf.RawToken,
                    refreshExpiresAt));
        }

        var confirmationEmail = await _dbContext.EmailVerificationTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == confirmation.TokenHash)
            .Join(
                _dbContext.Users.AsNoTracking(),
                token => token.UserId,
                user => user.Id,
                (_, user) => user.Email)
            .SingleOrDefaultAsync(cancellationToken);
        return confirmationEmail is null
            ? null
            : GoogleSignInStoreResult.ConfirmationRequired(
                new IssuedAuthenticationSecret(
                    confirmationEmail,
                    confirmation.RawToken,
                    confirmationExpiresAt));
    }

    /// <summary>Detecta se o link já foi persistido por outra tentativa concorrente.</summary>
    private async Task<GoogleLinkStoreStatus?> ResolveCompletedLinkAsync(
        Guid userId,
        VerifiedExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        var persisted = await _dbContext.Set<ExternalIdentity>()
            .AsNoTracking()
            .Where(candidate => candidate.Provider == identity.Provider &&
                (candidate.Subject == identity.Subject || candidate.UserId == userId))
            .Select(candidate => new { candidate.UserId, candidate.Subject })
            .ToListAsync(cancellationToken);
        if (persisted.Count == 0)
            return null;

        return persisted.Any(candidate => candidate.UserId == userId &&
            string.Equals(candidate.Subject, identity.Subject, StringComparison.Ordinal))
            ? GoogleLinkStoreStatus.Linked
            : GoogleLinkStoreStatus.IdentityConflict;
    }

    /// <summary>Materializa um refresh token opaco com CSRF associado.</summary>
    private static RefreshToken CreateRefresh(
        Guid userId,
        GeneratedOpaqueToken refresh,
        GeneratedOpaqueToken csrf,
        DateTime expiresAt,
        DateTime now) => new(
            userId,
            Guid.NewGuid(),
            refresh.TokenHash,
            csrf.TokenHash,
            null,
            expiresAt,
            now);

    /// <summary>Persiste mutações do sign-in, traduzindo conflitos de unicidade em concorrência.</summary>
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

    /// <summary>Estabelece o tenant efetivo uma única vez por tentativa de sign-in.</summary>
    private void EstablishOnce(
        AuthenticatedPrincipal principal,
        SignInAttemptState attemptState)
    {
        if (attemptState.IsTenantEstablished)
            return;

        _tenantInitializer.Establish(
            principal.TrainerId,
            principal.UserId,
            principal.Role,
            TenantOrigin.System,
            false);
        attemptState.IsTenantEstablished = true;
    }

    /// <summary>Identifica violação de unicidade nas constraints de external_identities.</summary>
    private static bool IsExternalIdentityConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
        postgres.ConstraintName is "uq_external_identities_provider_subject" or
            "uq_external_identities_user_provider";

    /// <summary>Identifica corrida concorrente na criação de utilizador com email já normalizado.</summary>
    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
        postgres.ConstraintName == "uq_users_normalized_email";

    private sealed class SignInAttemptState
    {
        internal RefreshToken? Refresh { get; set; }
        internal EmailVerificationToken? Confirmation { get; set; }
        internal TrainerSettings? TrainerSettings { get; set; }
        internal TrainerSubscription? Subscription { get; set; }
        internal bool IsTenantEstablished { get; set; }
    }

    private sealed class ExternalAuthenticationConcurrencyException : Exception;
}
