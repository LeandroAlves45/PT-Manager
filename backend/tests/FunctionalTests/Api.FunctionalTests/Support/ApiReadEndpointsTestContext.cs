namespace Api.FunctionalTests.Support;

public abstract class ApiReadEndpointsTestContext
{
    protected readonly PostgresApiFixture _fixture;

    protected ApiReadEndpointsTestContext(PostgresApiFixture fixture) => _fixture = fixture;
}
