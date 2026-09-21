using Infrastructure.Seeding;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>Registo do seed de desenvolvimento.</summary>
public static class DevelopmentSeedRegistration
{
    /// <summary>Regista o seed e o serviço que o corre no arranque.</summary>
    /// <remarks>
    /// Duas barreiras, não uma: o ambiente tem de ser Development **e** a configuração
    /// tem de o ligar explicitamente. Um seed que corra por omissão é um seed que um dia
    /// corre contra a base de dados errada.
    /// </remarks>
    public static IServiceCollection AddDevelopmentSeeding(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsDevelopment())
            return services;

        services
            .AddOptions<DevelopmentSeedOptions>()
            .Bind(configuration.GetSection(DevelopmentSeedOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Password),
                "DevelopmentSeed:Password is required when seed is enabled.")
            .ValidateOnStart();

        services.AddScoped<DevelopmentDataSeeder>();
        services.AddHostedService<DevelopmentSeedHostedService>();

        return services;
    }
}

/// <summary>Corre o seed uma vez, no arranque da aplicação.</summary>
internal sealed class DevelopmentSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DevelopmentSeedOptions _options;

    public DevelopmentSeedHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DevelopmentSeedOptions> options)
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
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();

        // A ativação é explícita. Depois de ativado, uma falha não pode ser escondida:
        // propagar a exceção impede que a API pareça pronta com dados incompletos.
        await seeder.SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
