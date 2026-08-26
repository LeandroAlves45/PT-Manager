using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>Emite e consome confirmações de email de uso único.</summary>
internal sealed class EmailConfirmationStore : IEmailConfirmationStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly IOpaqueTokenService _tokens;

    public EmailConfirmationStore(PtManagerDbContext dbContext, IOpaqueTokenService tokens)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public async Task<EmailConfirmationStoreResult> IssueAsync(
        Guid userId,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var generated = _tokens.Generate();
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Confirma um commit ambíguo antes de repetir a escrita.
            if (attempt > 1)
            {
                _dbContext.ChangeTracker.Clear();
                var committedEmail = await _dbContext.EmailVerificationTokens
                    .AsNoTracking()
                    .Where(token => token.TokenHash == generated.TokenHash)
                    .Join(_dbContext.Users,
                        token => token.UserId,
                        user => user.Id,
                        (token, user) => user.Email)
                    .SingleOrDefaultAsync(cancellationToken);
                if (committedEmail is not null)
                {
                    return EmailConfirmationStoreResult.Issued(
                        new IssuedAuthenticationSecret(
                            committedEmail,
                            generated.RawToken,
                            expiresAt));
                }
            }

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var user = await _dbContext.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (user is null)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.UserNotFound);
            if (!user.IsActive || user.IsDeleted)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.AccountInactive);
            if (user.EmailConfirmed)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.AlreadyConfirmed);

            var previous = await _dbContext.EmailVerificationTokens
                .Where(token => token.UserId == userId &&
                    token.ConsumedAt == null)
                .ToListAsync(cancellationToken);
            _dbContext.EmailVerificationTokens.RemoveRange(previous);

            _dbContext.EmailVerificationTokens.Add(new EmailVerificationToken(
                userId,
                generated.TokenHash,
                expiresAt,
                now
            ));

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return EmailConfirmationStoreResult.Issued(
                    new IssuedAuthenticationSecret(user.Email, generated.RawToken, expiresAt));
            }
            catch (DbUpdateConcurrencyException)
            {
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.ConcurrencyConflict);
            }
        });
    }

    public async Task<EmailConfirmationStoreResult> ConsumeAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var hash = _tokens.Hash(rawToken);
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Confirma um commit ambíguo antes de repetir a escrita: se o token
            // deste pedido já ficou consumido com o email confirmado, o commit
            // anterior teve sucesso.
            if (attempt > 1)
            {
                _dbContext.ChangeTracker.Clear();
                var committed = await _dbContext.EmailVerificationTokens
                    .AsNoTracking()
                    .Where(token => token.TokenHash == hash && token.ConsumedAt != null)
                    .Join(_dbContext.Users,
                        token => token.UserId,
                        user => user.Id,
                        (token, user) => user.EmailConfirmed)
                    .SingleOrDefaultAsync(cancellationToken);
                if (committed)
                    return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.Confirmed);
            }

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var token = await _dbContext.EmailVerificationTokens
                .FromSqlInterpolated(
                    $"SELECT * FROM email_verification_tokens WHERE token_hash = {hash} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (token is null)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.TokenNotFound);
            if (token.ConsumedAt is not null)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.TokenAlreadyConsumed);
            if (token.ExpiresAt <= now)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.TokenExpired);

            var user = await _dbContext.Users
                .SingleOrDefaultAsync(user => user.Id == token.UserId, cancellationToken);
            if (user is null)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.UserNotFound);
            if (!user.IsActive || user.IsDeleted)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.AccountInactive);
            if (user.EmailConfirmed)
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.AlreadyConfirmed);

            token.MarkConsumed(now);
            user.ConfirmEmail(now);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.Confirmed);
            }
            catch (DbUpdateConcurrencyException)
            {
                return EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.ConcurrencyConflict);
            }
        });
    }
}
