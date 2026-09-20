using Application.Features.Dashboard;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Dashboard;

/// <summary>
/// Lê os blocos do dashboard com um número fixo de comandos (14), independente do número de
/// clientes: cada bloco faz uma agregação e, quando mostra itens, um segundo SELECT com
/// <c>Take(5)</c>. Todos os blocos são limitados a clientes ativos do tenant.
/// </summary>
internal sealed class TrainerDashboardQueries : ITrainerDashboardQueries
{
    private readonly PtManagerDbContext _dbContext;

    public TrainerDashboardQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<TrainerDashboardData> GetAsync(
        Guid trainerId,
        TrainerDashboardWindow window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        var activeClients = _dbContext.Clients
            .AsNoTracking()
            .Where(client => client.OwnerTrainerId == trainerId && client.IsActive);

        var activeClientsCount = await activeClients.CountAsync(cancellationToken);

        var pendingReview = _dbContext.CheckIns
            .AsNoTracking()
            .Where(checkIn => checkIn.OwnerTrainerId == trainerId)
            .Where(checkIn =>
                checkIn.RespondedAt.HasValue &&
                !checkIn.CancelledAt.HasValue &&
                !checkIn.ReviewedAt.HasValue)
            .Where(checkIn => activeClients.Any(client => client.Id == checkIn.ClientId));

        // GroupBy constante e devolve total e atrasados num só comando.
        var pendingCounts = await pendingReview
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Overdue = group.Count(checkIn =>
                    checkIn.RespondedAt!.Value < window.ReviewOverdueBeforeUtc)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var pendingItems = await pendingReview
            .OrderBy(checkIn => checkIn.RespondedAt)
            .ThenBy(checkIn => checkIn.Id)
            .Take(DashboardThresholds.TopCount)
            .Select(checkIn => new TrainerDashboardDto.PendingReviewItemDto(
                checkIn.Id,
                checkIn.ClientId,
                activeClients
                    .Where(client => client.Id == checkIn.ClientId)
                    .Select(client => client.Name)
                    .First(),
                checkIn.CheckInDate,
                checkIn.RespondedAt!.Value))
            .ToListAsync(cancellationToken);

        var packsEnding = _dbContext.ClientSessionPacks
            .AsNoTracking()
            .Where(pack => pack.OwnerTrainerId == trainerId && pack.SessionsRemaining > 0)
            .Where(pack => activeClients.Any(client => client.Id == pack.ClientId))
            .Where(pack =>
                pack.SessionsRemaining <= DashboardThresholds.PackSessionsRemainingThreshold ||
                pack.ExpectedEndDate.HasValue && pack.ExpectedEndDate.Value <= window.PackEndingUntil);

        var packsEndingCount = await packsEnding.CountAsync(cancellationToken);
        var packItems = await packsEnding
            .OrderBy(pack => pack.ExpectedEndDate)
            .ThenBy(pack => pack.SessionsRemaining)
            .ThenBy(pack => pack.Id)
            .Take(DashboardThresholds.TopCount)
            .Select(pack => new TrainerDashboardDto.EndingPackItemDto(
                pack.Id,
                pack.ClientId,
                activeClients
                    .Where(client => client.Id == pack.ClientId)
                    .Select(client => client.Name)
                    .First(),
                pack.PackName,
                pack.SessionsTotal,
                pack.SessionsRemaining,
                pack.ExpectedEndDate))
            .ToListAsync(cancellationToken);

        var trainingPlansExpiring = _dbContext.TrainingPlans
            .AsNoTracking()
            .Where(plan => plan.OwnerTrainerId == trainerId && plan.IsActive && !plan.IsArchived)
            .Where(plan =>
                plan.EndDate.HasValue &&
                plan.EndDate.Value >= window.LocalToday &&
                plan.EndDate.Value <= window.PlanExpiringUntil)
            .Where(plan => activeClients.Any(client => client.Id == plan.ClientId));

        var trainingPlanCount = await trainingPlansExpiring.CountAsync(cancellationToken);
        var trainingPlanItems = await trainingPlansExpiring
            .OrderBy(plan => plan.EndDate)
            .ThenBy(plan => plan.Id)
            .Take(DashboardThresholds.TopCount)
            .Select(plan => new TrainerDashboardDto.ExpiringPlanItemDto(
                plan.Id,
                plan.ClientId,
                activeClients
                    .Where(client => client.Id == plan.ClientId)
                    .Select(client => client.Name)
                    .First(),
                plan.Name,
                plan.EndDate!.Value))
            .ToListAsync(cancellationToken);

        var mealPlansExpiring = _dbContext.MealPlans
            .AsNoTracking()
            .Where(plan => plan.OwnerTrainerId == trainerId && plan.IsActive && !plan.IsArchived)
            .Where(plan =>
                plan.EndsDate.HasValue &&
                plan.EndsDate.Value >= window.LocalToday &&
                plan.EndsDate.Value <= window.PlanExpiringUntil)
            .Where(plan => activeClients.Any(client => client.Id == plan.ClientId));

        var mealPlanCount = await mealPlansExpiring.CountAsync(cancellationToken);
        var mealPlanItems = await mealPlansExpiring
            .OrderBy(plan => plan.EndsDate)
            .ThenBy(plan => plan.Id)
            .Take(DashboardThresholds.TopCount)
            .Select(plan => new TrainerDashboardDto.ExpiringPlanItemDto(
                plan.Id,
                plan.ClientId,
                activeClients
                    .Where(client => client.Id == plan.ClientId)
                    .Select(client => client.Name)
                    .First(),
                plan.Name,
                plan.EndsDate!.Value))
            .ToListAsync(cancellationToken);

        var sessionsToday = _dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.OwnerTrainerId == trainerId)
            .Where(session =>
                session.StartsAt >= window.TodayStartUtc &&
                session.StartsAt < window.TodayEndUtc)
            .Where(session => activeClients.Any(client => client.Id == session.ClientId));

        var sessionAggregate = await sessionsToday
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                NextStartsAt = group
                    .Where(session =>
                        session.Status == Domain.ValueObjects.SessionStatus.Scheduled &&
                        session.StartsAt >= window.NowUtc)
                    .Min(session => (DateTimeOffset?)session.StartsAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var sessionItems = await sessionsToday
            .OrderBy(session => session.StartsAt)
            .ThenBy(session => session.Id)
            .Take(DashboardThresholds.TopCount)
            .Select(session => new TrainerDashboardDto.TodaySessionItemDto(
                session.Id,
                session.ClientId,
                activeClients
                    .Where(client => client.Id == session.ClientId)
                    .Select(client => client.Name)
                    .First(),
                session.StartsAt,
                session.DurationMinutes,
                session.SessionType,
                session.Location,
                session.Status.Value))
            .ToListAsync(cancellationToken);

        var withoutPlan = activeClients
            .Where(client => !_dbContext.TrainingPlans.Any(plan =>
                plan.ClientId == client.Id &&
                plan.OwnerTrainerId == trainerId &&
                plan.IsActive));

        var withoutPlanCount = await withoutPlan.CountAsync(cancellationToken);
        // "Sem plano desde" = última alteração de um plano antigo; quem nunca teve plano conta
        // desde a criação da ficha ("cliente nova"). A projeção intermédia é anónima porque o
        // EF não sabe ordenar por um membro de um record construído na projeção.
        var withoutPlanRows = await withoutPlan
            .Select(client => new
            {
                client.Id,
                client.Name,
                HasHadPlan = _dbContext.TrainingPlans.Any(plan =>
                    plan.ClientId == client.Id && plan.OwnerTrainerId == trainerId),
                SinceUtc = _dbContext.TrainingPlans
                    .Where(plan => plan.ClientId == client.Id && plan.OwnerTrainerId == trainerId)
                    .Max(plan => (DateTime?)plan.UpdatedAt) ?? client.CreatedAt
            })
            .OrderBy(row => row.SinceUtc)
            .ThenBy(row => row.Id)
            .Take(DashboardThresholds.TopCount)
            .ToListAsync(cancellationToken);

        var withoutPlanItems = withoutPlanRows
            .Select(row => new ClientWithoutPlanRow(
                row.Id,
                row.Name,
                row.HasHadPlan,
                row.SinceUtc))
            .ToList();

        var packSales = await _dbContext.ClientSessionPacks
            .AsNoTracking()
            .Where(pack => pack.OwnerTrainerId == trainerId)
            .Where(pack =>
                pack.PurchaseDate >= window.PreviousMonthStart &&
                pack.PurchaseDate < window.NextMonthStart)
            .GroupBy(pack => new
            {
                Month = pack.PurchaseDate < window.CurrentMonthStart
                    ? window.PreviousMonthStart
                    : window.CurrentMonthStart,
                pack.Currency
            })
            .Select(group => new PackSalesRow(
                group.Key.Month,
                group.Key.Currency,
                group.Sum(pack => (long)pack.PriceCents),
                group.Count()))
            .ToListAsync(cancellationToken);

        return new TrainerDashboardData(
            activeClientsCount,
            new TrainerDashboardDto.CheckInsPendingReviewDto(
                pendingCounts?.Total ?? 0,
                pendingCounts?.Overdue ?? 0,
                pendingItems),
            new TrainerDashboardDto.PacksEndingDto(packsEndingCount, packItems),
            new TrainerDashboardDto.PlansExpiringDto(
                trainingPlanCount + mealPlanCount,
                trainingPlanCount,
                mealPlanCount,
                trainingPlanItems,
                mealPlanItems),
            new TrainerDashboardDto.SessionsTodayDto(
                sessionAggregate?.Total ?? 0,
                sessionAggregate?.NextStartsAt,
                sessionItems),
            withoutPlanCount,
            withoutPlanItems,
            packSales);
    }
}
