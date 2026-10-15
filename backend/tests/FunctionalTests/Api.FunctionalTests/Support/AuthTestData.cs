using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Semeia contas e convites pelo host real e lê os emails de autenticação que o adapter
/// gravou. Partilhado pelos testes funcionais do onboarding.
/// </summary>
internal static partial class AuthTestData
{
    internal const string Password = "Functional-Password-1!";

    private static readonly DateTime SeedInstant = TrainerTenantSeeder.SeedInstant;

    /// <summary>Cria uma conta com password real, sem ficha nem subscrição.</summary>
    internal static async Task<Guid> SeedAccountAsync(
        ApiWebApplicationFactory factory,
        string email,
        string role = "trainer",
        bool confirmed = true,
        bool active = true,
        bool withPassword = true,
        bool googleLinked = false)
    {
        var user = new User(new EmailAddress(email), role, "Seeded Account", SeedInstant);
        if (confirmed)
            user.ConfirmEmail(SeedInstant);

        await using var scope = factory.Services.CreateAsyncScope();
        if (withPassword)
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            user.SetPasswordHash(hasher.HashPassword(user, Password), SeedInstant);
        }

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        context.Users.Add(user);
        if (googleLinked)
            context.Set<ExternalIdentity>().Add(new ExternalIdentity(
                user.Id, ExternalIdentity.GoogleProvider, $"sub-{Guid.NewGuid():N}", SeedInstant));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (!active)
        {
            await context.Users
                .Where(candidate => candidate.Id == user.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(candidate => candidate.IsActive, false),
                    TestContext.Current.CancellationToken);
        }

        return user.Id;
    }

    /// <summary>Cria treinador, ficha ativa sem conta e um convite válido para <paramref name="email"/>.</summary>
    internal static async Task<InvitationSeed> SeedInvitationAsync(
        ApiWebApplicationFactory factory,
        string discriminator,
        string? email = null,
        DateTime? expiresAt = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            factory, discriminator, cancellationToken);
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            factory, trainer.TrainerId, "Invited Client", cancellationToken);
        var inviteEmail = email ?? $"{discriminator}-client-{Guid.NewGuid():N}@client.test";

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(trainer.TrainerId, trainer.TrainerId, "trainer", TenantOrigin.System, false);
        var generated = scope.ServiceProvider
            .GetRequiredService<IOpaqueTokenService>()
            .Generate();
        var invitation = new InviteToken(
            trainer.TrainerId,
            clientId,
            new EmailAddress(inviteEmail),
            generated.TokenHash,
            // O store compara com o relógio real: ancorar no instante de seed expirava sozinho.
            expiresAt ?? DateTime.UtcNow.AddDays(7),
            expiresAt.HasValue ? expiresAt.Value.AddSeconds(-1) : SeedInstant);

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        context.InviteTokens.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);

        // Como em produção, a ficha tem como contacto o email do convite: o registo recusa um
        // convite cujo email já não é o do contacto.
        var address = new EmailAddress(inviteEmail);
        await context.Clients.IgnoreQueryFilters()
            .Where(candidate => candidate.Id == clientId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.ContactEmail, address.Value)
                    .SetProperty(candidate => candidate.NormalizedContactEmail, address.Normalized),
                cancellationToken);

        return new InvitationSeed(
            trainer.TrainerId, clientId, invitation.Id, inviteEmail, generated.RawToken);
    }

    /// <summary>Destinatário do email gravado pelo adapter.</summary>
    internal static string Recipient(RecordingHttpMessageHandler.RecordedRequest request) =>
        Parse(request).GetProperty("to")[0].GetString()!;

    /// <summary>Corpo em texto simples do email gravado.</summary>
    internal static string Text(RecordingHttpMessageHandler.RecordedRequest request) =>
        Parse(request).GetProperty("text").GetString()!;

    /// <summary>Assunto do email gravado.</summary>
    internal static string Subject(RecordingHttpMessageHandler.RecordedRequest request) =>
        Parse(request).GetProperty("subject").GetString()!;

    /// <summary>Token em claro do link do email, ou <c>null</c> se o email não tem token.</summary>
    internal static string? TokenOf(RecordingHttpMessageHandler.RecordedRequest request)
    {
        var match = TokenInLink().Match(Text(request));
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    private static JsonElement Parse(RecordingHttpMessageHandler.RecordedRequest request) =>
        JsonDocument.Parse(request.Body).RootElement;

    [GeneratedRegex(@"token=(\S+)")]
    private static partial Regex TokenInLink();
}

internal sealed record InvitationSeed(
    Guid TrainerId,
    Guid ClientId,
    Guid InviteId,
    string Email,
    string RawToken);
