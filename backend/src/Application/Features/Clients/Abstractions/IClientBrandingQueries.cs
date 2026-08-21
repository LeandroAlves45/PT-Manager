using Application.Features.Clients.Dtos;

namespace Application.Features.Clients.Abstractions;

/// <summary>Resolve o branding do portal a partir do cliente autenticado.</summary>
public interface IClientBrandingQueries
{
    Task<ClientBrandingDto?> GetAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken
    );
}
