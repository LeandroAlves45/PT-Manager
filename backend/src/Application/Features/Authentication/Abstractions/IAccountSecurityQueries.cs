using Application.Features.Authentication.Dtos;

namespace Application.Features.Authentication.Abstractions;

/// <summary>Lê os métodos de autenticação da conta autenticada.</summary>
public interface IAccountSecurityQueries
{
    /// <summary>Devolve <c>null</c> quando a conta não existe, está inativa ou foi apagada.</summary>
    Task<AccountSecurityDto?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
