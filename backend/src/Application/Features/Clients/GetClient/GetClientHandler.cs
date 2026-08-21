using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Results;

namespace Application.Features.Clients.GetClient;

/// <summary>Obtém um cliente e todos os packs utilizáveis no dia atual.</summary>
public sealed class GetClientHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClientQueries _clientQueries;

    /// <summary>Inicializa a query de detalhe.</summary>
    public GetClientHandler(
        ITenantContext tenantContext,
        IClientQueries clientQueries)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clientQueries);

        _tenantContext = tenantContext;
        _clientQueries = clientQueries;
    }

    /// <summary>Devolve NotFound para inexistente e cross-tenant.</summary>
    public async Task<Result<ClientDetailsDto>> HandleAsync(
        GetClientQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ClientId == Guid.Empty)
            return Result<ClientDetailsDto>.Failure(ClientErrors.ClientIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientDetailsDto>.Failure(actor.Error!);

        var dto = await _clientQueries.GetDetailsAsync(
            query.ClientId,
            cancellationToken);

        return dto is null
            ? Result<ClientDetailsDto>.Failure(ClientErrors.ClientNotFound)
            : Result<ClientDetailsDto>.Success(dto);
    }
}
