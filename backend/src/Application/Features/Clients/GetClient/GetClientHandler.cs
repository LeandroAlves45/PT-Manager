using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Results;

namespace Application.Features.Clients.GetClient;

/// <summary>Obtém um cliente e todos os packs utilizáveis no dia atual.</summary>
public sealed class GetClientHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientQueries _clientQueries;

    /// <summary>Inicializa a query de detalhe.</summary>
    public GetClientHandler(
        ITenantContext tenantContext,
        IClock clock,
        IClientQueries clientQueries)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(clientQueries);

        _tenantContext = tenantContext;
        _clock = clock;
        _clientQueries = clientQueries;
    }

    /// <summary>Devolve NotFound para inexistente e cross-tenant.</summary>
    public async Task<Result<ClientDetailsDto>> HandleAsync(
        GetClientQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ClientId == Guid.Empty)
        {
            var error = Error.Validation(new List<ValidationError>
            {
                new ValidationError(
                    Field: "ClientId",
                    Code: "client_id_required",
                    Message: "Client ID is required."
                )
            });
            return Result<ClientDetailsDto>.Failure(error);
        }

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (tenant.IsFailure)
            return Result<ClientDetailsDto>.Failure(tenant.Error!);

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var dto = await _clientQueries.GetDetailsAsync(
            query.ClientId,
            today,
            cancellationToken);

        if (dto is null)
            return Result<ClientDetailsDto>.Failure(ClientErrors.ClientNotFound);

        return Result<ClientDetailsDto>.Success(dto);
    }
}
