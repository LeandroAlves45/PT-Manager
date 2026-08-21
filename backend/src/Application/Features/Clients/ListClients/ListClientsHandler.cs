using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clientQueries = clientQueries ?? throw new ArgumentNullException(nameof(clientQueries));
    }

    /// <summary>Valida e devolve uma página projetada.</summary>
    public async Task<Result<PageResult<ClientSummaryDto>>> HandleAsync(
        ListClientsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);

        if (!validation.IsValid)
            return Result<PageResult<ClientSummaryDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<ClientSummaryDto>>.Failure(actor.Error!);

        var result = await _clientQueries.ListAsync(
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<ClientSummaryDto>>.Success(result);
    }
}
