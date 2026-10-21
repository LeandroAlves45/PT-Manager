using Application.Features.Authentication.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

/// <summary>
/// Usa o mesmo <see cref="AuthenticationLinkBuilder"/> e a mesma base do frontend que os emails,
/// para o link copiado ser exatamente o enviado.
/// </summary>
internal sealed class AuthenticationLinkFactory : IAuthenticationLinkFactory
{
    private readonly Uri _frontendBaseUrl;

    public AuthenticationLinkFactory(IOptions<ResendOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _frontendBaseUrl = options.Value.FrontendBaseUrl
            ?? throw new ArgumentException("Resend:FrontendBaseUrl is required.", nameof(options));
    }

    public string ClientInvitation(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return AuthenticationLinkBuilder.Build(
            _frontendBaseUrl, AuthenticationLinkBuilder.AcceptInvitePath, rawToken);
    }
}
