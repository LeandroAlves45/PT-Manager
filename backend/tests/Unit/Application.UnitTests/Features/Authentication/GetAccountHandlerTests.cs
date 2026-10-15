using Application.Errors;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Features.Authentication.GetAccount;

namespace Application.UnitTests.Features.Authentication;

public sealed class GetAccountHandlerTests
{
    [Fact]
    public async Task AuthenticatedUser_ReturnsTheStoredAccountSecurity()
    {
        var userId = Guid.NewGuid();
        var queries = new AccountSecurityQueriesStub(
            new AccountSecurityDto("trainer@example.test", "trainer", true, false));
        var handler = new GetAccountHandler(
            new TestTenantContext { UserId = userId, TrainerId = userId, Role = "trainer" },
            queries);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, queries.RequestedUserId);
        Assert.Equal("trainer@example.test", result.Value.Email);
        Assert.True(result.Value.HasPassword);
        Assert.False(result.Value.GoogleLinked);
    }

    [Fact]
    public async Task AnonymousContext_IsUnauthenticatedWithoutQuerying()
    {
        var queries = new AccountSecurityQueriesStub(null);
        var handler = new GetAccountHandler(new TestTenantContext(), queries);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(CommonErrors.UnauthenticatedUser.Code, result.Error!.Code);
        Assert.Null(queries.RequestedUserId);
    }

    [Fact]
    public async Task MissingOrInactiveAccount_IsUnauthenticated()
    {
        var handler = new GetAccountHandler(
            new TestTenantContext { UserId = Guid.NewGuid(), Role = "client" },
            new AccountSecurityQueriesStub(null));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(CommonErrors.UnauthenticatedUser.Code, result.Error!.Code);
    }

    private sealed class AccountSecurityQueriesStub(AccountSecurityDto? account) :
        IAccountSecurityQueries
    {
        public Guid? RequestedUserId { get; private set; }

        public Task<AccountSecurityDto?> GetAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            RequestedUserId = userId;
            return Task.FromResult(account);
        }
    }
}
