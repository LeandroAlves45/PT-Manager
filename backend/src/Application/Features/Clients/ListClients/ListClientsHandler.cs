using Application.Common.Abstractions;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Clients.ListClients;

/// <summary>Lista clientes com filtro e paginação determinística.</summary>
public sealed class ListClientsHandler
{
    private readonly IValidator<ListClientsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClientQueries _clientQueries;

    /// <summary>Inicializa a listagem com validator, tenant e porta de leitura.</summary>
    public ListClientsHandler(
        IValidator<ListClientsQuery> validator,
        ITenantContext tenantContext,
        IClientQueries clientQueries)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clientQueries);

        _validator = validator;
        _tenantContext = tenantContext;
        _clientQueries = clientQueries;
    }

    /// <summary>Valida e devolve uma página projetada.</summary>
    public async Task<Result<PageResult<ClientSummaryDto>>> HandleAsync(
        ListClientsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);

        if (!validation.IsValid)
            return Result<PageResult<ClientSummaryDto>>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (tenant.IsFailure)
            return Result<PageResult<ClientSummaryDto>>.Failure(tenant.Error!);

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var page = new PageRequest(query.PageNumber, query.PageSize);

        var result = await _clientQueries.ListAsync(
            search,
            query.Activity,
            page,
            cancellationToken);

        return Result<PageResult<ClientSummaryDto>>.Success(result);
    }
}
