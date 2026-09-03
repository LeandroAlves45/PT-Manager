using System.Net;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Contract;

/// <summary>
/// Prova que cada família de rotas recusa os atores errados.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ActorMatrixTests
{
    private readonly PostgresApiFixture _fixture;

    public ActorMatrixTests(PostgresApiFixture fixture) => _fixture = fixture;

    public static TheoryData<string> TrainerOnlyRoutes() =>
    [
        "/api/v1/clients",
        "/api/v1/trainer-settings",
        "/api/v1/foods",
        "/api/v1/meal-plans",
        "/api/v1/exercises",
        "/api/v1/training-plans",
        "/api/v1/sessions",
        "/api/v1/pack-types",
        "/api/v1/client-session-packs",
        "/api/v1/check-ins",
        "/api/v1/supplements",
        "/api/v1/supplement-assignments"
    ];

    public static TheoryData<string> SuperuserOnlyRoutes() =>
    [
        "/api/v1/global-foods",
        "/api/v1/global-exercises",
        "/api/v1/global-supplements"
    ];

    public static TheoryData<string> ClientOnlyRoutes() =>
    [
        "/api/v1/portal/branding",
        "/api/v1/portal/my-plan",
        "/api/v1/portal/my-nutrition",
        "/api/v1/portal/my-profile",
        "/api/v1/portal/my-supplements"
    ];

    [Theory]
    [MemberData(nameof(TrainerOnlyRoutes))]
    public async Task TrainerRoutes_RejectClientAndSuperuser(string route)
    {
        await AssertForbiddenAsync(
            route, TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));
        await AssertForbiddenAsync(
            route, TestJwtFactory.IssueSuperuser(Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(SuperuserOnlyRoutes))]
    public async Task GlobalRoutes_RejectTrainerAndClient(string route)
    {
        await AssertForbiddenAsync(
            route, TestJwtFactory.IssueTrainer(Guid.NewGuid()));
        await AssertForbiddenAsync(
            route, TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(ClientOnlyRoutes))]
    public async Task PortalRoutes_RejectTrainerAndSuperuser(string route)
    {
        await AssertForbiddenAsync(
            route, TestJwtFactory.IssueTrainer(Guid.NewGuid()));
        await AssertForbiddenAsync(
            route, TestJwtFactory.IssueSuperuser(Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(TrainerOnlyRoutes))]
    [MemberData(nameof(SuperuserOnlyRoutes))]
    [MemberData(nameof(ClientOnlyRoutes))]
    public async Task AllBusinessRoutes_RejectAnonymous(string route)
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await client.GetAsync(route, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            header => header.Scheme == "Bearer");
    }

    private async Task AssertForbiddenAsync(string route, string accessToken)
    {
        var client = _fixture.Factory.CreateOriginClient().WithBearer(accessToken);

        var response = await client.GetAsync(route, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
