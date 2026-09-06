using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Identity;

/// <summary>
/// Prova constraints e nulabilidade contra PostgreSQL real. InMemory aceitaria
/// duplicados e password_hash NOT NULL, pelo que não substitui estes testes.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExternalIdentityPersistenceTests(PostgresContainerFixture database)
{
    private static readonly DateTime Now =
        new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExternalUser_NullPasswordHash_Persists()
    {
        // A conta criada por Google nunca tem password: a migration tem de ter tornado
        // users.password_hash nullable.
        var user = CreateUser();
        await using var context = database.CreateContext(null);
        context.Users.Add(user);
        context.Set<ExternalIdentity>().Add(
            new ExternalIdentity(user.Id, "google", $"sub-{Guid.NewGuid():N}", Now));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();
        var stored = await context.Users.SingleAsync(
            candidate => candidate.Id == user.Id,
            TestContext.Current.CancellationToken);
        Assert.Null(stored.PasswordHash);
    }

    [Fact]
    public async Task SameProviderAndSubject_ForTwoUsers_IsRejected()
    {
        var first = CreateUser();
        var second = CreateUser();
        var subject = $"sub-{Guid.NewGuid():N}";
        await using var context = database.CreateContext(null);
        context.Users.AddRange(first, second);
        context.Set<ExternalIdentity>().AddRange(
            new ExternalIdentity(first.Id, "google", subject, Now),
            new ExternalIdentity(second.Id, "google", subject, Now));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("uq_external_identities_provider_subject",
            exception.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameProvider_ForOneUserTwice_IsRejected()
    {
        var user = CreateUser();
        await using var context = database.CreateContext(null);
        context.Users.Add(user);
        context.Set<ExternalIdentity>().AddRange(
            new ExternalIdentity(user.Id, "google", $"sub-{Guid.NewGuid():N}", Now),
            new ExternalIdentity(user.Id, "google", $"sub-{Guid.NewGuid():N}", Now));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("uq_external_identities_user_provider",
            exception.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateNonceHash_IsRejected()
    {
        var hash = new string('A', 64);
        await using var context = database.CreateContext(null);
        context.Set<ExternalAuthenticationChallenge>().AddRange(
            new ExternalAuthenticationChallenge(hash, "sign_in", null, Now.AddMinutes(5), Now),
            new ExternalAuthenticationChallenge(hash, "sign_in", null, Now.AddMinutes(5), Now));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("uq_external_auth_challenges_nonce_hash",
            exception.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeletingUser_CascadesExternalIdentity()
    {
        var user = CreateUser();
        var identityId = Guid.Empty;
        await using (var seed = database.CreateContext(null))
        {
            var identity = new ExternalIdentity(
                user.Id, "google", $"sub-{Guid.NewGuid():N}", Now);
            identityId = identity.Id;
            seed.Users.Add(user);
            seed.Set<ExternalIdentity>().Add(identity);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await database.ExecuteSqlAsync(
            "DELETE FROM users WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", user.Id));

        await using var context = database.CreateContext(null);
        var survivors = await context.Set<ExternalIdentity>()
            .AsNoTracking()
            .CountAsync(
                identity => identity.Id == identityId,
                TestContext.Current.CancellationToken);
        Assert.Equal(0, survivors);
    }

    [Fact]
    public async Task ChallengeWithMismatchedActor_IsRejectedByCheckConstraint()
    {
        // A entidade já bloqueia esta combinação; o teste prova que a base de dados
        // continua a bloqueá-la mesmo perante escrita fora do domínio.
        var user = CreateUser();
        await using (var seed = database.CreateContext(null))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            database.ExecuteSqlAsync(
                """
                INSERT INTO external_authentication_challenges
                    (id, nonce_hash, purpose, user_id, created_at, expires_at)
                VALUES
                    (@id, @hash, 'sign_in', @userId, now(), now() + interval '5 minutes')
                """,
                TestContext.Current.CancellationToken,
                new NpgsqlParameter("id", Guid.NewGuid()),
                new NpgsqlParameter("hash", new string('B', 64)),
                new NpgsqlParameter("userId", user.Id)));

        Assert.Contains("ck_external_auth_challenges_actor",
            exception.Message, StringComparison.Ordinal);
    }

    private static User CreateUser() => new(
        new EmailAddress($"google-{Guid.NewGuid():N}@example.test"),
        "trainer",
        "Google Test",
        Now);
}
