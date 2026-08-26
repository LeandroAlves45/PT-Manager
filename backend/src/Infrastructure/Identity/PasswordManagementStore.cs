using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>Altera passwords e revoga sessões no mesmo commit.</summary>
internal sealed class PasswordManagementStore : IPasswordManagementStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly IOpaqueTokenService _tokens;

    public PasswordManagementStore(
        PtManagerDbContext dbContext,
        UserManager<User> userManager,
        IOpaqueTokenService tokens)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public async Task<PasswordManagementStoreResult> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var newSecurityStamp = Guid.NewGuid().ToString();
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Confirma um commit ambíguo antes de repetir a escrita: o stamp é
            // único por chamada, logo prova que o commit anterior teve sucesso.
            if (attempt > 1)
            {
                _dbContext.ChangeTracker.Clear();
                if (await _dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(user => user.Id == userId &&
                        user.SecurityStamp == newSecurityStamp, cancellationToken))
                {
                    return PasswordManagementStoreResult.Changed();
                }
            }

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var user = await LockUserAsync(userId, cancellationToken);
            if (user is null)
                return PasswordManagementStoreResult.Failure(
                    PasswordManagementStoreStatus.UserNotFound);
            if (!await _userManager.CheckPasswordAsync(user, currentPassword))
                return PasswordManagementStoreResult.Failure(
                    PasswordManagementStoreStatus.CurrentPasswordInvalid);

            return await ApplyAsync(
                user,
                null,
                newPassword,
                newSecurityStamp,
                now,
                transaction,
                cancellationToken);
        });
    }

    public async Task<PasswordManagementStoreResult> ResetAsync(
        string rawToken,
        string newPassword,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var hash = _tokens.Hash(rawToken);
        var newSecurityStamp = Guid.NewGuid().ToString();
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Confirma um commit ambíguo antes de repetir a escrita: o stamp é
            // único por chamada, logo prova que o commit anterior teve sucesso.
            if (attempt > 1)
            {
                _dbContext.ChangeTracker.Clear();
                var committed = await _dbContext.PasswordResetTokens
                    .AsNoTracking()
                    .Where(token => token.TokenHash == hash && token.ConsumedAt != null)
                    .Join(_dbContext.Users,
                        token => token.UserId,
                        user => user.Id,
                        (token, user) => user.SecurityStamp)
                    .SingleOrDefaultAsync(cancellationToken);
                if (committed == newSecurityStamp)
                    return PasswordManagementStoreResult.Changed();
            }

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var token = await _dbContext.PasswordResetTokens
                .FromSqlInterpolated(
                    $"SELECT * FROM password_reset_tokens WHERE token_hash = {hash} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (token is null)
                return PasswordManagementStoreResult.Failure(
                    PasswordManagementStoreStatus.ResetTokenNotFound);
            if (token.ConsumedAt is not null)
                return PasswordManagementStoreResult.Failure(
                    PasswordManagementStoreStatus.ResetTokenConsumed);
            if (token.ExpiresAt <= now)
                return PasswordManagementStoreResult.Failure(
                    PasswordManagementStoreStatus.ResetTokenExpired);

            var user = await LockUserAsync(token.UserId, cancellationToken);
            if (user is null)
                return PasswordManagementStoreResult.Failure(
                    PasswordManagementStoreStatus.UserNotFound);

            return await ApplyAsync(
                user,
                token,
                newPassword,
                newSecurityStamp,
                now,
                transaction,
                cancellationToken);
        });
    }

    private async Task<PasswordManagementStoreResult> ApplyAsync(
        User user,
        PasswordResetToken? resetToken,
        string newPassword,
        string newSecurityStamp,
        DateTime now,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(_userManager, user, newPassword);
            if (!validation.Succeeded)
                return PasswordManagementStoreResult.Failure(
                    PasswordManagementStoreStatus.NewPasswordInvalid);
        }
        user.SetPasswordHash(_userManager.PasswordHasher.HashPassword(user, newPassword), now);
        user.SetSecurityStamp(newSecurityStamp, now);
        user.RotateConcurrencyStamp(now);
        resetToken?.MarkConsumed(now);

        var sessions = await _dbContext.RefreshTokens
            .Where(session => session.UserId == user.Id &&
                session.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.Revoke(now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PasswordManagementStoreResult.Changed();
        }
        catch (DbUpdateConcurrencyException)
        {
            return PasswordManagementStoreResult.Failure(
                PasswordManagementStoreStatus.ConcurrencyConflict);
        }
    }

    private Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
}
