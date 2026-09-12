using System.Text.Json;
using Application.Features.ClientPortal.Abstractions;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.ClientPortal;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.ClientPortal;

/// <summary>
/// Prova em PostgreSQL real os pontos 5, 7 e 8 do Gate 5C para o avatar:
/// concorrência não elimina o asset ativo, a escrita é tenant-safe, e a
/// constraint de par impede referências impossíveis de eliminar.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MyAvatarStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
    private const string DeletionType = "client-avatar.delete";

    private readonly PostgresContainerFixture _fixture;

    public MyAvatarStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Replace_WhenNoAvatarExists_WritesPairWithoutSchedulingDeletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(cancellationToken);

        var outcome = await ReplaceAsync(tenant, "a1", Guid.NewGuid(), cancellationToken);

        Assert.Equal(MyAvatarStatus.Updated, outcome.Status);
        Assert.Equal(Url("a1"), outcome.Profile!.AvatarUrl);
        Assert.Equal(("a1", 0), (await StoredPublicIdAsync(tenant, cancellationToken), await DeletionCountAsync(tenant, cancellationToken)));
    }

    [Fact]
    public async Task Replace_WhenAvatarExists_SchedulesDeletionOfThePreviousAssetInTheSameTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(cancellationToken);
        await ReplaceAsync(tenant, "a1", Guid.NewGuid(), cancellationToken);
        var correlationId = Guid.NewGuid();

        await ReplaceAsync(tenant, "a2", correlationId, cancellationToken);

        await using var context = _fixture.CreateAdministrativeContext();
        var message = await context.OutboxMessages.SingleAsync(
            candidate => candidate.TrainerId == tenant.TrainerId && candidate.MessageType == DeletionType,
            cancellationToken);
        Assert.Equal(PublicId("a1"), ReadPublicId(message.Payload));
        Assert.Equal($"{DeletionType}:{correlationId:N}", message.IdempotencyKey);
        Assert.Equal("a2", await StoredPublicIdAsync(tenant, cancellationToken));
    }

    [Fact]
    public async Task Replace_WhenTheSamePairIsReplayed_DoesNotScheduleDeletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(cancellationToken);
        await ReplaceAsync(tenant, "a1", Guid.NewGuid(), cancellationToken);

        await ReplaceAsync(tenant, "a1", Guid.NewGuid(), cancellationToken);

        Assert.Equal(0, await DeletionCountAsync(tenant, cancellationToken));
    }

    [Fact]
    public async Task Remove_ClearsPairAndSchedulesDeletion_AndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(cancellationToken);
        await ReplaceAsync(tenant, "a1", Guid.NewGuid(), cancellationToken);

        var first = await RemoveAsync(tenant, cancellationToken);
        var second = await RemoveAsync(tenant, cancellationToken);

        Assert.Equal(MyAvatarStatus.Updated, first.Status);
        Assert.Null(first.Profile!.AvatarUrl);
        Assert.Equal(MyAvatarStatus.Updated, second.Status);
        Assert.Null(await StoredPublicIdAsync(tenant, cancellationToken));
        Assert.Equal(1, await DeletionCountAsync(tenant, cancellationToken));
    }

    /// <summary>
    /// Ponto 5: dois escritores concorrentes ficam serializados pelo lock da
    /// ficha. Cada um agenda a eliminação do asset que encontrou ativo, e nenhum
    /// agenda a eliminação do asset que acaba por ficar ativo.
    /// </summary>
    [Fact]
    public async Task Replace_WhenTwoWritersRace_NeverSchedulesDeletionOfTheWinningAsset()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(cancellationToken);
        await ReplaceAsync(tenant, "initial", Guid.NewGuid(), cancellationToken);

        await Task.WhenAll(
            ReplaceAsync(tenant, "writer-a", Guid.NewGuid(), cancellationToken),
            ReplaceAsync(tenant, "writer-b", Guid.NewGuid(), cancellationToken));

        var winner = await StoredPublicIdAsync(tenant, cancellationToken);
        Assert.Contains(winner, new[] { "writer-a", "writer-b" });

        await using var context = _fixture.CreateAdministrativeContext();
        var scheduled = (await context.OutboxMessages
                .Where(message => message.TrainerId == tenant.TrainerId && message.MessageType == DeletionType)
                .Select(message => message.Payload)
                .ToListAsync(cancellationToken))
            .Select(ReadPublicId)
            .ToArray();

        Assert.Equal(2, scheduled.Length);
        Assert.Contains(PublicId("initial"), scheduled);
        Assert.DoesNotContain(PublicId(winner!), scheduled);
    }

    /// <summary>
    /// Ponto 7: a conta de um cliente só resolve a sua ficha dentro do tenant a
    /// que pertence. Com o tenant de outro trainer não há ficha, e nada é escrito.
    /// </summary>
    [Fact]
    public async Task Replace_WithAnotherTenant_FindsNothingAndWritesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await SeedAsync(cancellationToken);
        var otherTrainer = await SeedAsync(cancellationToken);

        await using var context = _fixture.CreateContext(otherTrainer.TrainerId);
        var outcome = await new MyAvatarStore(context).ReplaceAsync(
            otherTrainer.TrainerId, owner.ClientUserId, Url("intruder"), PublicId("intruder"),
            Guid.NewGuid(), Now, cancellationToken);

        Assert.Equal(MyAvatarStatus.NotFound, outcome.Status);
        Assert.Null(await StoredPublicIdAsync(owner, cancellationToken));
        Assert.Equal(0, await DeletionCountAsync(otherTrainer, cancellationToken));
    }

    /// <summary>
    /// Ponto 8: a base de dados recusa um URL sem identificador mesmo quando a
    /// escrita não passa pelo Domain.
    /// </summary>
    [Fact]
    public async Task Database_RejectsAnAvatarUrlWithoutPublicId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await SeedAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => _fixture.ExecuteSqlAsync(
            "UPDATE clients SET avatar_url = 'https://res.cloudinary.com/x.webp' WHERE id = @id",
            cancellationToken,
            new NpgsqlParameter("id", tenant.ClientId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_clients_avatar_pair", exception.ConstraintName);
    }

    private Task<PostgresContainerFixture.TestTenantSeed> SeedAsync(CancellationToken cancellationToken) =>
        _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);

    private async Task<MyAvatarOutcome> ReplaceAsync(
        PostgresContainerFixture.TestTenantSeed tenant,
        string asset,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        return await new MyAvatarStore(context).ReplaceAsync(
            tenant.TrainerId, tenant.ClientUserId, Url(asset), PublicId(asset),
            correlationId, Now, cancellationToken);
    }

    private async Task<MyAvatarOutcome> RemoveAsync(
        PostgresContainerFixture.TestTenantSeed tenant,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        return await new MyAvatarStore(context).RemoveAsync(
            tenant.TrainerId, tenant.ClientUserId, Guid.NewGuid(), Now, cancellationToken);
    }

    private async Task<string?> StoredPublicIdAsync(
        PostgresContainerFixture.TestTenantSeed tenant,
        CancellationToken cancellationToken)
    {
        var publicId = await _fixture.QueryScalarAsync<string>(
            "SELECT avatar_public_id FROM clients WHERE id = @id",
            cancellationToken,
            new NpgsqlParameter("id", tenant.ClientId));
        return publicId?.Replace("pt-manager/avatars/", string.Empty, StringComparison.Ordinal);
    }

    private async Task<int> DeletionCountAsync(
        PostgresContainerFixture.TestTenantSeed tenant,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateAdministrativeContext();
        return await context.OutboxMessages.CountAsync(
            message => message.TrainerId == tenant.TrainerId && message.MessageType == DeletionType,
            cancellationToken);
    }

    private static string Url(string asset) => $"https://res.cloudinary.com/demo/image/upload/{asset}.webp";

    private static string PublicId(string asset) => $"pt-manager/avatars/{asset}";

    private static string ReadPublicId(string payload) =>
        JsonDocument.Parse(payload).RootElement.GetProperty("public_id").GetString()!;
}
