using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Features.Administration.ContentModeration.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Administration.ContentModeration.ListModerationQueue;

/// <summary>
/// Lista o conteúdo privado de todos os personal trainers para o superuser poder bloquear sem
/// conhecer o id. Leitura administrativa: fica só no log estruturado do controller, sem
/// entrada de auditoria persistida (mesma decisão já tomada para os vídeos).
/// </summary>
public sealed class ListModerationQueueHandler
{
    private readonly IValidator<ListModerationQueueQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IModerationQueueQueries _queries;

    public ListModerationQueueHandler(
        IValidator<ListModerationQueueQuery> validator,
        ITenantContext tenantContext,
        IModerationQueueQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<ModerationQueueItemDto>>> HandleAsync(
        ListModerationQueueQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<ModerationQueueItemDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            ContentModerationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<ModerationQueueItemDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            query.Kind,
            query.Status,
            SearchTerm.Normalize(query.Search),
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<ModerationQueueItemDto>>.Success(page);
    }
}
