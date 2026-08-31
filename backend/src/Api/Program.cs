using Api;
using Api.Configuration;
using Api.Middlewares;
using Application;
using Infrastructure;
using Scalar.AspNetCore;

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
builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();
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
}

app.MapControllers();

app.Run();

/// <summary>Expõe o entry point apenas à WebApplicationFactory.</summary>
public partial class Program;
