using Application.Features.Administration.Trainers;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Features.Administration.Trainers.SetBillingExemption;
using Application.Features.Billing.Abstractions;
using Application.Pagination;
using Application.UnitTests.Features.Authentication;

namespace Application.UnitTests.Features;

/// <summary>
/// Isentar cancela primeiro a subscrição paga no Stripe (U42); se o Stripe falhar, nada é
/// gravado e o treinador continua como estava.
/// </summary>
public sealed class SetBillingExemptionHandlerTests : ReadHandlerTestContext
{
    private static readonly Guid Trainer = Guid.NewGuid();

    [Fact]
    public async Task Exempting_WithAPaidSubscription_CancelsItBeforeSaving()
    {
        var log = new List<string>();
        var handler = Create(log, "sub_paid", BillingGatewayStatus.Success);

        var result = await handler.HandleAsync(new SetBillingExemptionCommand(Trainer, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["cancel:sub_paid", "store:True"], log);
    }

    [Fact]
    public async Task Exempting_WhenStripeFails_DoesNotSave()
    {
        var log = new List<string>();
        var handler = Create(log, "sub_paid", BillingGatewayStatus.TransientFailure);

        var result = await handler.HandleAsync(new SetBillingExemptionCommand(Trainer, true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TrainerAdministrationErrors.PaymentProviderUnavailable.Code, result.Error!.Code);
        Assert.Equal(["cancel:sub_paid"], log);
    }

    [Fact]
    public async Task Exempting_WithoutAPaidSubscription_DoesNotCallStripe()
    {
        var log = new List<string>();
        var handler = Create(log, chargeable: null, BillingGatewayStatus.Success);

        await handler.HandleAsync(new SetBillingExemptionCommand(Trainer, true), CancellationToken.None);

        Assert.Equal(["store:True"], log);
    }

    [Fact]
    public async Task RemovingTheExemption_NeverCallsStripe()
    {
        var log = new List<string>();
        var handler = Create(log, "sub_paid", BillingGatewayStatus.Success);

        await handler.HandleAsync(new SetBillingExemptionCommand(Trainer, false), CancellationToken.None);

        Assert.Equal(["store:False"], log);
    }

    [Fact]
    public async Task TrainerActor_IsForbidden()
    {
        var log = new List<string>();
        var handler = new SetBillingExemptionHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new QueriesStub("sub_paid"),
            new StoreStub(log),
            new CancellationStub(log, BillingGatewayStatus.Success),
            new TestClock(DateTime.UtcNow));

        var result = await handler.HandleAsync(new SetBillingExemptionCommand(Trainer, true), CancellationToken.None);

        Assert.Equal(TrainerAdministrationErrors.AdministratorOnly.Code, result.Error!.Code);
        Assert.Empty(log);
    }

    private static SetBillingExemptionHandler Create(
        List<string> log,
        string? chargeable,
        BillingGatewayStatus cancellation) =>
        new(
            new AdministrativeTenantStub(),
            new QueriesStub(chargeable),
            new StoreStub(log),
            new CancellationStub(log, cancellation),
            new TestClock(DateTime.UtcNow));

    private sealed class QueriesStub(string? chargeable) : ITrainerAdministrationQueries
    {
        public Task<TrainerLookupDto> LookupByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AdminTrainerDto?> GetTrainerAsync(Guid trainerId, CancellationToken cancellationToken) =>
            Task.FromResult<AdminTrainerDto?>(new AdminTrainerDto(
                trainerId, "Ana", "ana@x.pt", "ACTIVE", "PRO", true, DateTime.UtcNow, false, 0, DateTime.UtcNow, null));

        public Task<PageResult<AdminTrainerDto>> ListExemptAsync(PageRequest page, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PendingTrainerInvitationDto>> ListPendingInvitationsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> FindChargeableStripeSubscriptionAsync(Guid trainerId, CancellationToken cancellationToken) =>
            Task.FromResult(chargeable);
    }

    private sealed class StoreStub(List<string> log) : ITrainerAdministrationStore
    {
        public Task<TrainerAdministrationStoreStatus> SetBillingExemptionAsync(
            Guid actorUserId, Guid trainerId, bool isExempt, DateTime now, CancellationToken cancellationToken)
        {
            log.Add($"store:{isExempt}");
            return Task.FromResult(TrainerAdministrationStoreStatus.Changed);
        }

        public Task<IssueTrainerInvitationStoreResult> IssueInvitationAsync(
            Guid actorUserId, string email, string tokenHash, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TrainerAdministrationStoreStatus> RevokeInvitationAsync(
            Guid actorUserId, Guid invitationId, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TrainerAdministrationStoreStatus> BlockAccessAsync(
            Guid actorUserId, Guid trainerId, Domain.ValueObjects.PlatformEnforcementReason reason,
            string? note, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TrainerAdministrationStoreStatus> UnblockAccessAsync(
            Guid actorUserId, Guid trainerId, DateTime now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CancellationStub(List<string> log, BillingGatewayStatus outcome)
        : ISubscriptionCancellationGateway
    {
        public Task<BillingGatewayStatus> CancelImmediatelyAsync(
            string providerSubscriptionId, CancellationToken cancellationToken)
        {
            log.Add($"cancel:{providerSubscriptionId}");
            return Task.FromResult(outcome);
        }
    }
}
