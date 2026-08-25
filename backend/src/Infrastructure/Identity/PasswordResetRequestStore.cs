using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>Emite credenciais de reset sem permitir enumeração de contas.</summary>
public sealed class PasswordResetRequestStore : IPasswordResetRequestStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly IOpaqueTokenService _tokens;
    private readonly ILookupNormalizer _normalizer;

    public PasswordResetRequestStore(
        PtManagerDbContext dbContext,
        IOpaqueTokenService tokens,
        ILookupNormalizer normalizer)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
    }

    public async Task<PasswordResetRequestStoreResult> IssueAsync(
        string email,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var normalized = _normalizer.NormalizeEmail(email);
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var user = await _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE normalized_email = {normalized} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || !user.IsActive || user.IsDeleted || !user.EmailConfirmed)
            return PasswordResetRequestStoreResult.For(PasswordResetRequestStoreStatus.NotEligible);

        var previous = await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id &&
                token.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        _dbContext.PasswordResetTokens.RemoveRange(previous);

        var generated = _tokens.Generate();
        _dbContext.PasswordResetTokens.Add(new PasswordResetToken(
            user.Id,
            generated.TokenHash,
            expiresAt,
            now
        ));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PasswordResetRequestStoreResult.Issued(
                new IssuedAuthenticationSecret(user.Email, generated.RawToken, expiresAt));
        }
        catch (DbUpdateConcurrencyException)
        {
            return PasswordResetRequestStoreResult.For(
                PasswordResetRequestStoreStatus.ConcurrencyConflict);
        }
    }
}
