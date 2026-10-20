using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.TrainerInvitations;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.Entities.TrainerSettings;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>
/// Consome o convite de treinador isento. O link prova a posse do email, por isso a conta nasce
/// confirmada; nasce já no Pro isento, sem checkout (RN-008).
/// </summary>
internal sealed class TrainerInvitationAcceptanceStore : ITrainerInvitationAcceptanceStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly ITenantContextInitializer _tenantInitializer;
    private readonly IOpaqueTokenService _tokens;

    public TrainerInvitationAcceptanceStore(
        PtManagerDbContext dbContext,
        UserManager<User> userManager,
        ITenantContextInitializer tenantInitializer,
        IOpaqueTokenService tokens)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tenantInitializer = tenantInitializer ?? throw new ArgumentNullException(nameof(tenantInitializer));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public async Task<TrainerInvitationStatusResult> GetStatusAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hash = _tokens.Hash(rawToken);
        var invitation = await _dbContext.TrainerInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        var status = Classify(invitation, now);
        return new TrainerInvitationStatusResult(
            status,
            status == TrainerInvitationStatus.Valid ? invitation!.Email : null);
    }

    public async Task<TrainerInvitationStatus> RegisterAsync(
        string rawToken,
        string fullName,
        string password,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hash = _tokens.Hash(rawToken);

        // Leitura sem bloqueio só para construir o utilizador e estabelecer o tenant uma única
        // vez, fora do delegate repetível; as decisões repetem-se com bloqueio na transação.
        var preview = await _dbContext.TrainerInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);
        var previewStatus = Classify(preview, now);
        if (previewStatus != TrainerInvitationStatus.Valid)
            return previewStatus;

        var email = new EmailAddress(preview!.Email);
        var user = new User(email, "trainer", fullName, now);
        user.ConfirmEmail(now);
        _tenantInitializer.Establish(user.Id, user.Id, "trainer", TenantOrigin.System, false);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Confirma um commit ambíguo antes de repetir a escrita.
            if (attempt > 1)
            {
                _dbContext.ChangeTracker.Clear();
                if (await _dbContext.Users.AsNoTracking().AnyAsync(
                    candidate => candidate.Id == user.Id, cancellationToken))
                    return TrainerInvitationStatus.Registered;
            }

            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var invitation = await _dbContext.TrainerInvitations
                .FromSqlInterpolated(
                    $"SELECT * FROM trainer_invitations WHERE token_hash = {hash} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var status = Classify(invitation, now);
            if (status != TrainerInvitationStatus.Valid)
                return status;

            if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(
                candidate => candidate.NormalizedEmail == email.Normalized,
                cancellationToken))
                return TrainerInvitationStatus.AccountAlreadyExists;

            var created = await _userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return created.IsOnlyDuplicateAccount()
                    ? TrainerInvitationStatus.AccountAlreadyExists
                    : TrainerInvitationStatus.PasswordRejected;
            }

            // O construtor exige um trial futuro; a isenção a seguir retira-o e passa a Pro.
            var subscription = new TrainerSubscription(user.Id, now.AddDays(1), now);
            subscription.SetBillingExemption(true, now);
            _dbContext.TrainerSubscriptions.Add(subscription);
            _dbContext.TrainerSettings.Add(new TrainerSettings(user.Id, now));
            invitation!.MarkUsed(now);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TrainerInvitationStatus.ConcurrencyConflict;
            }

            return TrainerInvitationStatus.Registered;
        });
    }

    // Usado ganha a expirado: quem já criou a conta deve ser mandado entrar com ela.
    private static TrainerInvitationStatus Classify(TrainerInvitation? invitation, DateTime now) =>
        invitation switch
        {
            null => TrainerInvitationStatus.Invalid,
            { IsRevoked: true } => TrainerInvitationStatus.Invalid,
            { IsUsed: true } => TrainerInvitationStatus.Used,
            _ when invitation.ExpiresAt <= now => TrainerInvitationStatus.Expired,
            _ => TrainerInvitationStatus.Valid
        };
}
