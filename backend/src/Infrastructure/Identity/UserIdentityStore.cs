using Application.Common.Abstractions;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Identity;

/// <summary>Custom Identity store suportado pelo modelo User do Domain.</summary>
internal sealed class UserIdentityStore :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserSecurityStampStore<User>,
    IUserLockoutStore<User>
{
    private readonly PtManagerDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IdentityErrorDescriber _errors;

    public UserIdentityStore(
        PtManagerDbContext dbContext,
        IClock clock,
        IdentityErrorDescriber errors)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        _dbContext.Users.Add(user);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (DbUpdateException exception)
            when (IsDuplicateEmail(exception))
        {
            _dbContext.Entry(user).State = EntityState.Detached;
            return IdentityResult.Failed(_errors.DuplicateEmail(user.Email));
        }
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        user.RotateConcurrencyStamp(_clock.UtcNow);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return IdentityResult.Failed(_errors.ConcurrencyFailure());
        }
        catch (DbUpdateException exception)
            when (IsDuplicateEmail(exception))
        {
            return IdentityResult.Failed(_errors.DuplicateEmail(user.Email));
        }
    }

    /// <inheritdoc/>
    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.SoftDelete(_clock.UtcNow);
        return await UpdateAsync(user, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? _dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
            : Task.FromResult<User?>(null);

    /// <inheritdoc/>
    public Task<User?> FindByNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken) =>
        _dbContext.Users.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalizedUserName,
            cancellationToken);

    /// <inheritdoc/>
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        FindByNameAsync(normalizedEmail, cancellationToken);

    /// <inheritdoc/>
    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(RequireUser(user).Id.ToString());

    /// <inheritdoc/>
    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(RequireUser(user).Email);

    /// <inheritdoc/>
    public Task SetUserNameAsync(
        User user,
        string? userName,
        CancellationToken cancellationToken)
    {
        RequireUser(user).SetEmail(new EmailAddress(userName ?? string.Empty), _clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string?> GetNormalizedUserNameAsync(
        User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(RequireUser(user).NormalizedEmail);

    /// <inheritdoc/>
    public Task SetNormalizedUserNameAsync(
        User user,
        string? normalizedUserName,
        CancellationToken cancellationToken)
    {
        EnsureNormalizedValue(RequireUser(user), normalizedUserName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetPasswordHashAsync(
        User user,
        string? passwordHash,
        CancellationToken cancellationToken)
    {
        RequireUser(user).SetPasswordHash(passwordHash ?? string.Empty, _clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string?> GetPasswordHashAsync(
        User user,
        CancellationToken cancellationToken) =>
        Task.FromResult(RequireUser(user).PasswordHash);

    /// <inheritdoc/>
    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(RequireUser(user).PasswordHash));

    /// <inheritdoc/>
    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        RequireUser(user).SetEmail(new EmailAddress(email ?? string.Empty), _clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(RequireUser(user).Email);

    /// <inheritdoc/>
    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(RequireUser(user).EmailConfirmed);

    /// <inheritdoc/>
    public Task SetEmailConfirmedAsync(
        User user,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (!confirmed)
            throw new NotSupportedException("Reverting a confirmed email is not supported.");
        RequireUser(user).ConfirmEmail(_clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(RequireUser(user).NormalizedEmail);

    /// <inheritdoc/>
    public Task SetNormalizedEmailAsync(
        User user,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        EnsureNormalizedValue(RequireUser(user), normalizedEmail);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetSecurityStampAsync(
        User user,
        string stamp,
        CancellationToken cancellationToken)
    {
        RequireUser(user).SetSecurityStamp(stamp, _clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string?> GetSecurityStampAsync(
        User user,
        CancellationToken cancellationToken) =>
        Task.FromResult<string?>(RequireUser(user).SecurityStamp);

    /// <inheritdoc/>
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(
        User user,
        CancellationToken cancellationToken) =>
        Task.FromResult(RequireUser(user).LockoutEnd is { } value
            ? (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
            : null);

    /// <inheritdoc/>
    public Task SetLockoutEndDateAsync(
        User user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        RequireUser(user).SetLockoutEnd(lockoutEnd?.UtcDateTime, _clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> IncrementAccessFailedCountAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var value = RequireUser(user);
        value.RegisterFailedAccess(_clock.UtcNow);
        return Task.FromResult(value.AccessFailedCount);
    }

    /// <inheritdoc/>
    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        RequireUser(user).ResetFailedAccess(_clock.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(RequireUser(user).AccessFailedCount);

    /// <inheritdoc/>
    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <inheritdoc/>
    public Task SetLockoutEnabledAsync(
        User user,
        bool enabled,
        CancellationToken cancellationToken)
    {
        if (!enabled)
            throw new NotSupportedException("Local accounts always enable lockout.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose() { }

    private static User RequireUser(User? user) =>
        user ?? throw new ArgumentNullException(nameof(user));

    private static void EnsureNormalizedValue(User user, string? normalizedValue)
    {
        if (!string.Equals(user.NormalizedEmail, normalizedValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Identity normalization diverged from EmailAddress.");
    }

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
        postgres.ConstraintName == "uq_users_normalized_email";
}
