using System.Security.Cryptography;
using System.Text;
using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity;

/// <summary>Autentica, roda e revoga sessões locais.</summary>
internal sealed class AuthenticationSessionStore : IAuthenticationSessionStore
{
    private static readonly EventId LoginEvent = new(2001, "Login");
    private static readonly EventId LockoutEvent = new(2003, "Lockout");
    private static readonly EventId RefreshRotationEvent = new(2008, "RefreshRotation");
    private static readonly EventId RefreshReuseEvent = new(2009, "RefreshReuse");
    private static readonly EventId RefreshFamilyRevocationEvent = new(2010, "RefreshFamilyRevocation");
    private static readonly EventId CsrfRejectionEvent = new(2012, "CsrfRejection");

    private readonly PtManagerDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly ILookupNormalizer _normalizer;
    private readonly IOpaqueTokenService _tokens;
    private readonly ITenantContextInitializer _tenantInitializer;
    private readonly ILogger<AuthenticationSessionStore> _logger;

    public AuthenticationSessionStore(
        PtManagerDbContext dbContext,
        UserManager<User> userManager,
        ILookupNormalizer normalizer,
        IOpaqueTokenService tokens,
        ITenantContextInitializer tenantInitializer,
        ILogger<AuthenticationSessionStore> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _tenantInitializer = tenantInitializer ?? throw new ArgumentNullException(nameof(tenantInitializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AuthenticateStoreResult> AuthenticateAsync(
        string email,
        string password,
        DateTime now,
        DateTime refreshExpiresAt,
        CancellationToken cancellationToken
    )
    {
        var normalized = _normalizer.NormalizeEmail(email);
        var user = await _dbContext.Users.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalized,
            cancellationToken);
        if (user is null)
        {
            LogLoginRejection(AuthenticateStoreStatus.InvalidCredentials);
            return AuthenticateStoreResult.Failure(AuthenticateStoreStatus.InvalidCredentials);
        }
        if (!user.IsActive || user.IsDeleted)
        {
            LogLoginRejection(AuthenticateStoreStatus.AccountInactive);
            return AuthenticateStoreResult.Failure(AuthenticateStoreStatus.AccountInactive);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning(LockoutEvent,
                "Authentication login was rejected with outcome {SecurityOutcome}.",
                "locked_out");
            return AuthenticateStoreResult.Failure(AuthenticateStoreStatus.LockedOut);
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            var failed = await _userManager.AccessFailedAsync(user);
            var status = failed.Succeeded
                ? AuthenticateStoreStatus.InvalidCredentials
                : AuthenticateStoreStatus.ConcurrencyConflict;
            LogLoginRejection(status);
            return AuthenticateStoreResult.Failure(status);
        }

        if (!user.EmailConfirmed)
        {
            LogLoginRejection(AuthenticateStoreStatus.EmailNotConfirmed);
            return AuthenticateStoreResult.Failure(AuthenticateStoreStatus.EmailNotConfirmed);
        }

        var reset = await _userManager.ResetAccessFailedCountAsync(user);
        if (!reset.Succeeded)
        {
            LogLoginRejection(AuthenticateStoreStatus.ConcurrencyConflict);
            return AuthenticateStoreResult.Failure(AuthenticateStoreStatus.ConcurrencyConflict);
        }

        var principal = await ResolvePrincipalAsync(user, cancellationToken);
        if (principal is null)
        {
            LogLoginRejection(AuthenticateStoreStatus.RelationshipInactive);
            return AuthenticateStoreResult.Failure(AuthenticateStoreStatus.RelationshipInactive);
        }
        Establish(principal);

        var generated = _tokens.Generate();
        var csrf = _tokens.Generate();
        _dbContext.RefreshTokens.Add(new RefreshToken(
            user.Id,
            Guid.NewGuid(),
            generated.TokenHash,
            csrf.TokenHash,
            null,
            refreshExpiresAt,
            now
        ));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(LoginEvent,
                "Authentication login completed with outcome {SecurityOutcome} for user {UserId} and role {Role}.",
                "succeeded",
                principal.UserId,
                principal.Role);

            return AuthenticateStoreResult.Authenticated(
                principal,
                new IssuedRefreshSession(generated.RawToken, csrf.RawToken, refreshExpiresAt));
        }
        catch (DbUpdateConcurrencyException)
        {
            LogLoginRejection(AuthenticateStoreStatus.ConcurrencyConflict);
            return AuthenticateStoreResult.Failure(AuthenticateStoreStatus.ConcurrencyConflict);
        }
    }

    public async Task<RotateRefreshStoreResult> RotateAsync(
        string rawToken,
        string rawCsrfToken,
        DateTime now,
        DateTime refreshExpiresAt,
        CancellationToken cancellationToken
    )
    {
        var hash = _tokens.Hash(rawToken);
        var presentedCsrfHash = _tokens.Hash(rawCsrfToken);
        var generated = _tokens.Generate();
        var csrf = _tokens.Generate();
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        var established = false;

        var outcome = await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Confirma um commit ambíguo antes de repetir a escrita: o refresh
            // token emitido é único por chamada e prova o sucesso anterior.
            if (attempt > 1)
            {
                _dbContext.ChangeTracker.Clear();
                var committed = await RebuildCommittedRotationAsync(
                    generated,
                    csrf,
                    refreshExpiresAt,
                    cancellationToken);
                if (committed is not null)
                    return committed;
            }

            return await RotateOnceAsync(
                hash,
                presentedCsrfHash,
                generated,
                csrf,
                now,
                refreshExpiresAt,
                principal =>
                {
                    // O tenant só pode ser estabelecido uma vez por scope,
                    // mesmo quando a strategy repete o delegate.
                    if (established)
                        return;
                    Establish(principal);
                    established = true;
                },
                cancellationToken);
        });

        LogRefreshResult(outcome);
        return outcome;
    }

    public async Task<RotateCsrfStoreResult> RotateCsrfAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var hash = _tokens.Hash(rawToken);
        var csrf = _tokens.Generate();
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        var outcome = await strategy.ExecuteAsync(async () =>
        {
            // Ao contrário de RotateAsync, aqui não existe o problema do
            // commit ambíguo. A operação é idempotente na prática: repeti-la
            // apenas escreve outro CSRF na mesma linha, e o cliente recebe o
            // valor que a chamada bem sucedida devolveu.
            _dbContext.ChangeTracker.Clear();

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var current = await _dbContext.RefreshTokens
                .FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE token_hash = {hash} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (current is null)
                return RotateCsrfStoreResult.Failure(RotateCsrfStoreStatus.NotFound);
            if (current.IsReused())
            {
                await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return RotateCsrfStoreResult.Failure(RotateCsrfStoreStatus.Reused);
            }
            if (!current.IsActive(now))
                return RotateCsrfStoreResult.Failure(RotateCsrfStoreStatus.Expired);

            current.ReplaceCsrfTokenHash(csrf.TokenHash, now);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return RotateCsrfStoreResult.Rotated(csrf.RawToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RotateCsrfStoreResult.Failure(RotateCsrfStoreStatus.ConcurrencyConflict);
            }
        });

        if (outcome.Kind == RotateCsrfStoreStatus.Reused)
            LogRefreshReuseAndFamilyRevocation();

        return outcome;
    }

    private async Task<RotateRefreshStoreResult?> RebuildCommittedRotationAsync(
        GeneratedOpaqueToken generated,
        GeneratedOpaqueToken csrf,
        DateTime refreshExpiresAt,
        CancellationToken cancellationToken
    )
    {
        var committed = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == generated.TokenHash,
                cancellationToken);
        if (committed is null)
            return null;

        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == committed.UserId, cancellationToken);
        var principal = await ResolvePrincipalAsync(user, cancellationToken);
        if (principal is null)
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.PrincipalInvalid);

        return RotateRefreshStoreResult.Rotated(
            principal,
            new IssuedRefreshSession(generated.RawToken, csrf.RawToken, refreshExpiresAt));
    }

    private async Task<RotateRefreshStoreResult> RotateOnceAsync(
        string hash,
        string presentedCsrfHash,
        GeneratedOpaqueToken generated,
        GeneratedOpaqueToken csrf,
        DateTime now,
        DateTime refreshExpiresAt,
        Action<AuthenticatedPrincipal> establish,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var current = await _dbContext.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE token_hash = {hash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (current is null)
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.NotFound);
        if (current.IsReused())
        {
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.Reused);
        }

        if (!current.IsActive(now))
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.Expired);

        if (!MatchesCsrf(current.CsrfTokenHash, presentedCsrfHash))
        {
            _logger.LogWarning(CsrfRejectionEvent,
                "Refresh rotation was rejected with outcome {SecurityOutcome}.", "csrf_invalid");
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.CsrfInvalid);
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(user => user.Id == current.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.IsDeleted || !user.EmailConfirmed)
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.PrincipalInvalid);

        var principal = await ResolvePrincipalAsync(user, cancellationToken);
        if (principal is null)
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.PrincipalInvalid);
        establish(principal);

        current.Revoke(now);
        _dbContext.RefreshTokens.Add(new RefreshToken(
            user.Id,
            current.FamilyId,
            generated.TokenHash,
            csrf.TokenHash,
            current.Id,
            refreshExpiresAt,
            now
        ));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RotateRefreshStoreResult.Rotated(
                principal,
                new IssuedRefreshSession(generated.RawToken, csrf.RawToken, refreshExpiresAt));
        }
        catch (DbUpdateConcurrencyException)
        {
            return RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.ConcurrencyConflict);
        }
    }

    public async Task<RevokeSessionStoreStatus> RevokeAsync(
        string rawToken,
        string rawCsrfToken,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var hash = _tokens.Hash(rawToken);
        var presentedCsrfHash = _tokens.Hash(rawCsrfToken);
        var token = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (token is null || token.IsReused())
            return RevokeSessionStoreStatus.NotFound;

        if (!MatchesCsrf(token.CsrfTokenHash, presentedCsrfHash))
        {
            _logger.LogWarning(CsrfRejectionEvent,
                "Logout was rejected with outcome {SecurityOutcome}.", "csrf_invalid");
            return RevokeSessionStoreStatus.CsrfInvalid;
        }

        token.Revoke(now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RevokeSessionStoreStatus.Revoked;
    }

    public async Task RevokeAllAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
            token.Revoke(now);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool MatchesCsrf(string storedHash, string presentedHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(storedHash),
            Encoding.ASCII.GetBytes(presentedHash));

    private async Task<AuthenticatedPrincipal?> ResolvePrincipalAsync(
        User user,
        CancellationToken cancellationToken
    )
    {
        Guid? trainerId = user.Role switch
        {
            "trainer" => user.Id,
            "superuser" => null,
            "client" => await ResolveClientTrainerAsync(user.Id, cancellationToken),
            _ => null
        };
        if (user.Role != "superuser" && !trainerId.HasValue)
            return null;
        return new AuthenticatedPrincipal(user.Id, trainerId, user.Role, user.SecurityStamp);
    }

    private async Task<Guid?> ResolveClientTrainerAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var trainers = await _dbContext.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(client => client.UserId == userId && client.IsActive && !client.IsDeleted)
            .Select(client => client.OwnerTrainerId)
            .Take(2)
            .ToListAsync(cancellationToken);
        return trainers.Count == 1 ? trainers[0] : null;
    }

    private void Establish(AuthenticatedPrincipal principal) =>
        _tenantInitializer.Establish(
            principal.TrainerId,
            principal.UserId,
            principal.Role,
            TenantOrigin.System,
            false);

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var family = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in family)
            token.Revoke(now);

    }

    private void LogLoginRejection(AuthenticateStoreStatus status) =>
        _logger.LogWarning(LoginEvent,
            "Authentication login was rejected with outcome {SecurityOutcome}.",
            status.ToString());

    private void LogRefreshResult(RotateRefreshStoreResult result)
    {
        if (result.Kind == RotateRefreshStoreStatus.Rotated)
        {
            _logger.LogInformation(RefreshRotationEvent,
                "Refresh rotation completed with outcome {SecurityOutcome} for user {UserId} and role {Role}.",
                "succeeded",
                result.Principal!.UserId,
                result.Principal.Role);
            return;
        }

        if (result.Kind == RotateRefreshStoreStatus.Reused)
        {
            LogRefreshReuseAndFamilyRevocation();
            return;
        }

        _logger.LogWarning(RefreshRotationEvent,
            "Refresh rotation was rejected with outcome {SecurityOutcome}.", result.Kind.ToString());
    }

    private void LogRefreshReuseAndFamilyRevocation()
    {
        _logger.LogWarning(RefreshReuseEvent,
            "Refresh reuse was detected with outcome {SecurityOutcome}.", "family_revoked");
        _logger.LogWarning(RefreshFamilyRevocationEvent,
            "A refresh token family was revoked with outcome {SecurityOutcome}.", "reuse_detected");
    }
}
