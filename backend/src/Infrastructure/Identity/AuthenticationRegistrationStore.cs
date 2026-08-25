using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.Entities.TrainerSettings;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>Persiste o onboarding local completo numa transação.</summary>
public sealed class AuthenticationRegistrationStore : IAuthenticationRegistrationStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly ITenantContextInitializer _tenantInitializer;
    private readonly IOpaqueTokenService _tokens;
    private readonly IClock _clock;

    public AuthenticationRegistrationStore(
        PtManagerDbContext dbContext,
        UserManager<User> userManager,
        ITenantContextInitializer tenantInitializer,
        IOpaqueTokenService tokens,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tenantInitializer = tenantInitializer ?? throw new ArgumentNullException(nameof(tenantInitializer));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<RegisterTrainerStoreResult> RegisterTrainerAsync(
        RegisterTrainerStoreRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var generated = _tokens.Generate();
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var user = new User(
            new EmailAddress(request.Email),
            "trainer",
            request.FullName,
            now
        );

        var identityResult = await _userManager.CreateAsync(user, request.Password);
        if (!identityResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RegisterTrainerStoreResult.For(identityResult.Errors.Any(error =>
                error.Code == nameof(IdentityErrorDescriber.DuplicateEmail))
                ? RegisterTrainerStoreStatus.DuplicateEmail
                : RegisterTrainerStoreStatus.InvalidIdentityData);
        }

        _tenantInitializer.Establish(user.Id, user.Id, "trainer", TenantOrigin.System, false);
        _dbContext.TrainerSubscriptions.Add(new TrainerSubscription(
            user.Id,
            request.TrialEndsAt,
            now
        ));
        _dbContext.TrainerSettings.Add(new TrainerSettings(user.Id, now));
        _dbContext.EmailVerificationTokens.Add(new EmailVerificationToken(
            user.Id,
            generated.TokenHash,
            request.EmailConfirmationExpiresAt,
            now
        ));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RegisterTrainerStoreResult.For(RegisterTrainerStoreStatus.ConcurrencyConflict);
        }

        return RegisterTrainerStoreResult.Created(user.Id, user.Id,
            new IssuedAuthenticationSecret(
                user.Email,
                generated.RawToken,
                request.EmailConfirmationExpiresAt
            )
        );
    }
}
