using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Pagination;
using Domain.Entities.Administration;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Administration;

/// <summary>
/// Leituras do superuser sobre treinadores. Ignoram os filtros de tenant de propósito: o
/// contexto administrativo não tem tenant, e a projeção nunca expõe dados de clientes além
/// da contagem dos ativos.
/// </summary>
internal sealed class TrainerAdministrationQueries : ITrainerAdministrationQueries
{
    private readonly PtManagerDbContext _dbContext;

    public TrainerAdministrationQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<TrainerLookupDto> LookupByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var account = await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            // Uma conta apagada mantém o email (o convite também a recusa): aparece como «não
            // é treinador» em vez de «não encontrado», para o superuser não cair num 409.
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is not null)
        {
            // GetTrainerAsync já exclui contas apagadas: um treinador apagado devolve null.
            var trainer = account.Role == "trainer"
                ? await GetTrainerAsync(account.Id, cancellationToken)
                : null;
            return trainer is null
                ? new TrainerLookupDto(TrainerLookupStatus.NotTrainer, null, null)
                : new TrainerLookupDto(TrainerLookupStatus.Found, trainer, null);
        }

        var invitation = await PendingInvitations(normalizedEmail)
            .Select(pending => new PendingTrainerInvitationDto(
                pending.Id,
                pending.Email,
                pending.CreatedAt,
                pending.ExpiresAt))
            .FirstOrDefaultAsync(cancellationToken);

        return invitation is null
            ? new TrainerLookupDto(TrainerLookupStatus.NotFound, null, null)
            : new TrainerLookupDto(TrainerLookupStatus.InvitePending, null, invitation);
    }

    public Task<AdminTrainerDto?> GetTrainerAsync(Guid trainerId, CancellationToken cancellationToken) =>
        Project(TrainerRows().Where(row => row.User.Id == trainerId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PageResult<AdminTrainerDto>> ListExemptAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = TrainerRows().Where(row => row.Subscription.IsExemptFromBilling);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query
                .OrderByDescending(row => row.Subscription.BillingExemptSince)
                .ThenBy(row => row.User.Id)
                .Skip((page.PageNumber - 1) * page.PageSize)
                .Take(page.PageSize))
            .ToListAsync(cancellationToken);

        return new PageResult<AdminTrainerDto>(items, totalCount);
    }

    public async Task<IReadOnlyList<PendingTrainerInvitationDto>> ListPendingInvitationsAsync(
        CancellationToken cancellationToken) =>
        await PendingInvitations(normalizedEmail: null)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Select(invitation => new PendingTrainerInvitationDto(
                invitation.Id,
                invitation.Email,
                invitation.CreatedAt,
                invitation.ExpiresAt))
            .ToListAsync(cancellationToken);

    public Task<string?> FindChargeableStripeSubscriptionAsync(
        Guid trainerId,
        CancellationToken cancellationToken) =>
        _dbContext.TrainerSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(subscription => subscription.TrainerId == trainerId &&
                !subscription.IsExemptFromBilling &&
                subscription.StripeSubscriptionId != null &&
                subscription.Status != SubscriptionStatus.Cancelled &&
                subscription.Status != SubscriptionStatus.Inactive)
            .Select(subscription => subscription.StripeSubscriptionId)
            .FirstOrDefaultAsync(cancellationToken);

    // Filtros e ordenação aplicam-se às entidades: depois de projetar para o construtor do
    // record o EF já não consegue traduzir um Where ou um OrderBy.
    private IQueryable<TrainerRow> TrainerRows() =>
        from user in _dbContext.Users.IgnoreQueryFilters().AsNoTracking()
        join subscription in _dbContext.TrainerSubscriptions.IgnoreQueryFilters().AsNoTracking()
            on user.Id equals subscription.TrainerId
        where user.Role == "trainer" && !user.IsDeleted
        select new TrainerRow { User = user, Subscription = subscription };

    private IQueryable<AdminTrainerDto> Project(IQueryable<TrainerRow> rows) =>
        rows.Select(row => new AdminTrainerDto(
            row.User.Id,
            row.User.FullName,
            row.User.Email,
            row.Subscription.Status.Value,
            row.Subscription.Tier.Value,
            row.Subscription.IsExemptFromBilling,
            row.Subscription.BillingExemptSince,
            !row.Subscription.IsExemptFromBilling &&
                row.Subscription.StripeSubscriptionId != null &&
                row.Subscription.Status != SubscriptionStatus.Cancelled &&
                row.Subscription.Status != SubscriptionStatus.Inactive,
            _dbContext.Clients.IgnoreQueryFilters().Count(client =>
                client.OwnerTrainerId == row.User.Id && client.IsActive && !client.IsDeleted),
            row.User.CreatedAt,
            _dbContext.TrainerAccessBlocks
                .Where(block => block.TrainerId == row.User.Id)
                .Select(block => new TrainerAccessBlockDto(
                    block.Reason.Value,
                    block.Note,
                    block.BlockedAt,
                    block.BlockedAt.AddDays(TrainerAccessBlock.ClientGraceDays)))
                .FirstOrDefault()));

    private IQueryable<TrainerInvitation> PendingInvitations(string? normalizedEmail) =>
        _dbContext.TrainerInvitations
            .AsNoTracking()
            .Where(invitation => invitation.UsedAt == null && invitation.RevokedAt == null &&
                (normalizedEmail == null || invitation.NormalizedEmail == normalizedEmail));

    private sealed class TrainerRow
    {
        public User User { get; init; } = null!;
        public TrainerSubscription Subscription { get; init; } = null!;
    }
}
