using Api;
using Api.Configuration;
using Api.Middlewares;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// A API escreve para stdout; Windows Event Log exige permissões que não pertencem ao processo.
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Secção de configuração de serviços
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors(ApiCorsPolicy.PolicyName);

// A Fase 2 insere UseAuthentication() exatamente aqui: o limiter global particiona
// por utilizador autenticado e so consegue ler HttpContext.User depois da autenticacao.
app.UseRateLimiter();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

// TODO: Decisão aberta, não aprovada: expor /openapi/v1.json apenas em Development.
// Se Preview precisar do documento para contract tests ou geração de client,
// esta condição tem de ser revista antes do fecho da Fase 1.
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();

app.Run();

/// <summary>Expõe o entry point apenas à WebApplicationFactory.</summary>
public partial class Program;
