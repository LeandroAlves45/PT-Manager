using Application.Common.Abstractions;
using Application.Features.ClientPortal;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.DeleteMyAccount;

namespace Application.UnitTests.Features.ClientPortal;

/// <summary>
/// Só o próprio cliente elimina a conta, a palavra de confirmação é obrigatória e as
/// falhas de password não revelam se a conta está bloqueada.
/// </summary>
public sealed class DeleteMyAccountHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientUserId = Guid.NewGuid();

    [Fact]
    public async Task WhenActorIsTrainer_ReturnsForbiddenWithoutTouchingTheStore()
    {
        var store = new FakeStore();

        var result = await CreateHandler(store, role: "trainer").HandleAsync(
            new DeleteMyAccountCommand("password", "ELIMINAR"), TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.ClientOnly.Code, result.Error!.Code);
        Assert.False(store.WasCalled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("eliminar conta")]
    [InlineData("DELETE")]
    public async Task WithoutTheConfirmationWord_IsValidationErrorAndNothingIsDeleted(string confirmation)
    {
        var store = new FakeStore();

        var result = await CreateHandler(store).HandleAsync(
            new DeleteMyAccountCommand("password", confirmation), TestContext.Current.CancellationToken);

        Assert.Equal(
            "portal_account_deletion_confirmation_required",
            Assert.Single(result.Error!.ValidationErrors).Code);
        Assert.False(store.WasCalled);
    }

    [Theory]
    [InlineData("ELIMINAR")]
    [InlineData(" eliminar ")]
    public async Task WithTheConfirmationWord_DeletesTheAuthenticatedAccountOnly(string confirmation)
    {
        var store = new FakeStore();

        var result = await CreateHandler(store).HandleAsync(
            new DeleteMyAccountCommand("password", confirmation), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((TrainerId, ClientUserId, "password", Now), (store.TrainerId, store.ClientUserId, store.Password, store.Now));
    }

    [Fact]
    public async Task EmptyPassword_IsSentAsMissing()
    {
        var store = new FakeStore();

        await CreateHandler(store).HandleAsync(
            new DeleteMyAccountCommand(string.Empty, "ELIMINAR"), TestContext.Current.CancellationToken);

        Assert.Null(store.Password);
    }

    [Theory]
    [InlineData(MyAccountDeletionStatus.PasswordRequired)]
    [InlineData(MyAccountDeletionStatus.PasswordInvalid)]
    public async Task PasswordFailures_ShareOneFieldError(MyAccountDeletionStatus status)
    {
        var store = new FakeStore { Status = status };

        var result = await CreateHandler(store).HandleAsync(
            new DeleteMyAccountCommand("wrong", "ELIMINAR"), TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Error!.ValidationErrors);
        Assert.Equal(("Password", "portal_account_deletion_password_invalid"), (error.Field, error.Code));
    }

    [Fact]
    public async Task NoActiveProfile_IsNotFound()
    {
        var store = new FakeStore { Status = MyAccountDeletionStatus.NotFound };

        var result = await CreateHandler(store).HandleAsync(
            new DeleteMyAccountCommand("password", "ELIMINAR"), TestContext.Current.CancellationToken);

        Assert.Equal(ClientPortalErrors.ProfileNotAvailable.Code, result.Error!.Code);
    }

    private static DeleteMyAccountHandler CreateHandler(FakeStore store, string role = "client") =>
        new(new DeleteMyAccountCommandValidator(), new StubTenantContext(role), new StubClock(), store);

    private sealed class FakeStore : IMyAccountDeletionStore
    {
        public MyAccountDeletionStatus Status { get; init; } = MyAccountDeletionStatus.Deleted;
        public bool WasCalled { get; private set; }
        public Guid TrainerId { get; private set; }
        public Guid ClientUserId { get; private set; }
        public string? Password { get; private set; }
        public DateTime Now { get; private set; }

        public Task<MyAccountDeletionStatus> DeleteAsync(
            Guid trainerId,
            Guid clientUserId,
            string? password,
            Guid correlationId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            (TrainerId, ClientUserId, Password, Now) = (trainerId, clientUserId, password, now);
            return Task.FromResult(Status);
        }
    }

    private sealed class StubTenantContext(string role) : ITenantContext
    {
        public Guid? TrainerId => DeleteMyAccountHandlerTests.TrainerId;
        public Guid? UserId => ClientUserId;
        public string? Role => role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StubClock : IClock
    {
        public DateTime UtcNow => DeleteMyAccountHandlerTests.Now;
    }
}
