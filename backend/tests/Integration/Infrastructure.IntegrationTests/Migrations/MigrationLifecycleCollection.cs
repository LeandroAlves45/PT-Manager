namespace Infrastructure.IntegrationTests.Migrations;


/// <summary>
/// Partilha um único container e impede concorrência entre classes que alteram
/// o estado global da base de dados.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MigrationLifecycleCollection : ICollectionFixture<MigrationLifecycleFixture>
{
    public const string Name = "migration-lifecycle";
}
