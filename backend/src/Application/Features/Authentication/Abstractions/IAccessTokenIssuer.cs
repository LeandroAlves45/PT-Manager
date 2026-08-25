namespace Application.Features.Authentication.Abstractions;

/// <summary>Emite um access token para identidade acabada de validar.</summary>
public interface IAccessTokenIssuer
{
    /// <summary>Emite sem I/O depois do commit da refresh session.</summary>
    IssuedAccessToken Issue(AuthenticatedPrincipal principal);
}
