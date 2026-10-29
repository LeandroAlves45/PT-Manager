using Api;
using Api.Configuration;
using Api.Middlewares;
using Application;
using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos;
using Infrastructure;
using Infrastructure.Identity;
using Infrastructure.Jobs;
using Infrastructure.Seeding;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// A API escreve para stdout; Windows Event Log exige permissões que não pertencem ao processo.
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddJsonConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
    // CorrelationId, TraceId e SpanId (e JobId no dispatcher) em cada linha.
    options.IncludeScopes = true;
});
builder.Logging.AddDebug();
builder.AddApiObservability();

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Secção de configuração de serviços
builder.Services.AddApplication();
builder.Services.AddGoogleAuthenticationApplication();
builder.Services.AddJobDispatchApplication();
builder.Services.AddExerciseVideoApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGoogleAuthenticationInfrastructure(builder.Configuration);
builder.Services.AddJobDispatchInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration, builder.Environment);
builder.Services.AddGoogleAuthenticationApi();
builder.Services.AddInternalJobDispatchApi(builder.Configuration);
builder.Services.AddDevelopmentSeeding(builder.Configuration, builder.Environment);
builder.Services.AddSuperuserBootstrap(builder.Configuration);
builder.Services.AddApiHealthChecks();

var app = builder.Build();

// Antes do UseForwardedHeaders: o IP da ligação ainda é o do proxy, não o do cliente.
app.UseMiddleware<UntrustedProxyLoggingMiddleware>();
app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<DbCommandMetricsMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors(ApiCorsPolicy.PolicyName);

app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
// Depois da autorização: um papel errado continua a dar 403 antes de se ler a conta.
app.UseMiddleware<AccountAccessMiddleware>();

// A UI e o documento ficam restritos a Development: expor a superfície completa
// da API, incluindo as rotas administrativas, é reconhecimento gratuito para
// quem sonde o serviço. O teste OpenApiDocument_IsNotExposedOutsideDevelopment
// prova a restrição.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("PT Manager API")
        .AddPreferredSecuritySchemes("Bearer")
        .DisableAgent()
        .WithNonce());
    app.MapDevelopmentSessionReminders();
}

app.MapApiHealthChecks();
app.MapControllers();

app.Run();

/// <summary>Expõe o entry point apenas à WebApplicationFactory.</summary>
public partial class Program;
