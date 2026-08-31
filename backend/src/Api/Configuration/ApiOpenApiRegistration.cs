using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace Api.Configuration;

/// <summary>Regista o documento OpenAPI servido apenas em Development.</summary>
public static class ApiOpenApiRegistration
{
    /// <summary>Nome do esquema de segurança declarado no documento.</summary>
    private const string BearerSchemeName = "Bearer";

    /// <summary>Adiciona o documento OpenAPI com o esquema bearer declarado.</summary>
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddOperationTransformer((operation, context, _) =>
            {
                var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<IAuthorizeData>()
                    .Any();

                if (!requiresAuthorization)
                    return Task.CompletedTask;

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerSchemeName, context.Document)] = []
                });

                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "PT Manager API",
                    Version = "v1",
                    Description =
                        "Documento servido apenas em Development. Todas as rotas de "
                        + "autenticação exigem o header Origin."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??=
                    new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

                // Declarado como http/bearer e não apiKey. É o access token
                // que o /auth/login devolve no corpo, enviado em Authorization.
                // O refresh token nunca entra aqui: vive no cookie HttpOnly e a
                // UI não lhe deve tocar.
                document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description =
                        "Access token devolvido por POST /api/v1/auth/login. "
                        + "Introduzir apenas o token, sem o prefixo Bearer."
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
