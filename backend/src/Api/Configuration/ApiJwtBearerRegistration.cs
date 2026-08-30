using System.Text;
using Api.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Api.Configuration;

/// <summary>Regista e configura a validação de access tokens JWT.</summary>
public static class ApiJwtBearerRegistration
{
    private const string SectionName = "Jwt";

    /// <summary>Adiciona o esquema bearer com validação estrita.</summary>
    public static IServiceCollection AddApiJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName)
            ?? throw new InvalidOperationException("Jwt configuration is missing.");

        var issuer = Require(section["Issuer"], "Jwt:Issuer");
        var audience = Require(section["Audience"], "Jwt:Audience");
        var signingKey = ReadSigningKey(Require(section["SigningKey"], "Jwt:SigningKey"));
        var clockSkew = ReadClockSkew(section["ClockSkew"]);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Sem isto o handler reescreve "sub" para o URI longo de
                // ClaimTypes.NameIdentifier e "role" para o de ClaimTypes.Role.
                // O TenantContextMiddleware procura literalmente "sub" e "role".
                options.MapInboundClaims = false;

                // A API nunca serve páginas; um redirect para login seria um bug.
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    ValidateAudience = true,
                    ValidAudience = audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKey),

                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = clockSkew,

                    NameClaimType = ApiClaimNames.Subject,
                    RoleClaimType = ApiClaimNames.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.ErrorDescription = null;
                        context.Error = null;
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    private static string Require(string? value, string key) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Configuration '{key}' is required.")
            : value;

    private static byte[] ReadSigningKey(string value)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            key = Encoding.UTF8.GetBytes(value);
        }

        if (key.Length < 32)
            throw new InvalidOperationException(
                "Configuration 'Jwt:SigningKey' must provide at least 256 bits of key material.");

        return key;
    }

    private static TimeSpan ReadClockSkew(string? value)
    {
        if (value is null)
            return TimeSpan.FromSeconds(30);

        if (!TimeSpan.TryParse(value, out var parsed))
            throw new InvalidOperationException(
                "Configuration 'Jwt:ClockSkew' is invalid.");

        return parsed < TimeSpan.Zero || parsed > TimeSpan.FromSeconds(30)
            ? throw new InvalidOperationException(
                "Configuration 'Jwt:ClockSkew' cannot exceed 30 seconds.")
            : parsed;
    }
}
