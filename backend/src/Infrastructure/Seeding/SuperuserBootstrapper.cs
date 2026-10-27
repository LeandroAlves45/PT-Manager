using Application.Common.Abstractions;
using Domain.Entities.Identity;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Seeding;

/// <summary>
/// Cria o primeiro superuser de um ambiente novo, onde o seed de desenvolvimento não existe.
/// </summary>
/// <remarks>
/// <para>
/// É idempotente: se já existir qualquer superuser, não faz nada. Assim, deixar a opção ligada
/// por esquecimento não cria uma segunda conta nem repõe a password de quem já existe.
/// </para>
/// <para>
/// A password nunca é escrita nos logs. O email já fica confirmado porque o superuser não passa
/// pelo fluxo de confirmação: não há outro superuser que o possa convidar.
/// </para>
/// </remarks>
public sealed class SuperuserBootstrapper
{
    private const string FullName = "Administrador PT Manager";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SuperuserBootstrapOptions _options;
    private readonly ILogger<SuperuserBootstrapper> _logger;

    public SuperuserBootstrapper(
        IServiceScopeFactory scopeFactory,
        IOptions<SuperuserBootstrapOptions> options,
        ILogger<SuperuserBootstrapper> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Cria o superuser se ainda não existir nenhum. Devolve <c>true</c> quando criou.</summary>
    /// <exception cref="InvalidOperationException">O email configurado pertence a uma conta que não é superuser.</exception>
    public async Task<bool> EnsureAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(null, null, "superuser", TenantOrigin.System, isAdministrative: true);

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        if (await context.Users.AnyAsync(user => user.Role == "superuser", cancellationToken))
        {
            _logger.LogInformation("Superuser bootstrap skipped: a superuser already exists.");
            return false;
        }

        var email = new EmailAddress(_options.Email);

        if (await context.Users.AnyAsync(user => user.NormalizedEmail == email.Normalized, cancellationToken))
            throw new InvalidOperationException(
                "SuperuserBootstrap:Email belongs to an account that is not a superuser.");

        var now = DateTime.UtcNow;
        var superuser = new User(email, "superuser", FullName, now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        superuser.SetPasswordHash(hasher.HashPassword(superuser, _options.Password), now);
        superuser.ConfirmEmail(now);

        context.Users.Add(superuser);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Superuser created by bootstrap. Set SuperuserBootstrap:Enabled=false and remove the password from the environment.");
        return true;
    }
}
