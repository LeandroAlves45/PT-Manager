namespace Infrastructure.IntegrationTests.Support;

/// <summary>
/// Partilha um único container e impede concorrência entre classes que alteram
/// o estado global da base de dados.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "postgresql";
}
