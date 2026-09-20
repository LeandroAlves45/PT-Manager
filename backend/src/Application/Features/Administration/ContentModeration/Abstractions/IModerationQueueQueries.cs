using Application.Features.Administration.ContentModeration.Dtos;
using Application.Features.Administration.ContentModeration.ListModerationQueue;
using Application.Pagination;

namespace Application.Features.Administration.ContentModeration.Abstractions;

/// <summary>
/// Leitura transversal a tenants do conteúdo privado, exclusiva do superuser em contexto
/// administrativo. A implementação ignora os Global Query Filters de forma explícita e
/// limita sempre a <c>OwnerTrainerId != null</c>.
/// </summary>
public interface IModerationQueueQueries
{
    Task<PageResult<ModerationQueueItemDto>> ListAsync(
        ModerationContentKind kind,
        ModerationStatusFilter status,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken);
}
