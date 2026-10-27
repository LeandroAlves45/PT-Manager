using Infrastructure.Seeding;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>Registo da criação do primeiro superuser.</summary>
public static class SuperuserBootstrapRegistration
{
    /// <summary>Regista as opções e o serviço que cria o superuser no arranque, se estiver ligado.</summary>
    /// <remarks>
    /// Ao contrário do seed de desenvolvimento, funciona em qualquer ambiente, mas está desligado
    /// por omissão e só avança com uma password longa. As migrations têm de estar aplicadas antes
    /// de o ligar: a API não as aplica no arranque.
    /// </remarks>
    public static IServiceCollection AddSuperuserBootstrap(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SuperuserBootstrapOptions>()
            .Bind(configuration.GetSection(SuperuserBootstrapOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                $"SuperuserBootstrap:Email is required and SuperuserBootstrap:Password needs at least "
                + $"{SuperuserBootstrapOptions.MinimumPasswordLength} characters when the bootstrap is enabled.")
            .ValidateOnStart();

        services.AddScoped<SuperuserBootstrapper>();
        services.AddHostedService<SuperuserBootstrapHostedService>();

        return services;
    }
}

/// <summary>Corre a criação do superuser uma vez, no arranque da aplicação.</summary>
internal sealed class SuperuserBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SuperuserBootstrapOptions _options;

    public SuperuserBootstrapHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<SuperuserBootstrapOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<SuperuserBootstrapper>();

        // Depois de ligado, uma falha não se esconde: a API não deve parecer pronta sem o
        // superuser que o operador pediu.
        await bootstrapper.EnsureAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
