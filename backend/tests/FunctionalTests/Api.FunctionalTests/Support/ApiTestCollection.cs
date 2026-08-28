namespace Api.FunctionalTests.Support;

/// <summary>Partilha um PostgreSQL por coleção sem criar dependência entre testes.</summary>
[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<PostgresApiFixture>
{
    public const string Name = "api_postgres";
}
