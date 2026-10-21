namespace Application.Features.Authentication.Abstractions;

/// <summary>
/// Constrói, para a resposta da API, o mesmo link que o email leva. Serve o «Copiar link» do
/// convite: o token cru só existe no momento da emissão, a base de dados guarda o hash.
/// </summary>
public interface IAuthenticationLinkFactory
{
    /// <summary>Link de aceitação do convite do cliente, com o token em query string.</summary>
    string ClientInvitation(string rawToken);
}
