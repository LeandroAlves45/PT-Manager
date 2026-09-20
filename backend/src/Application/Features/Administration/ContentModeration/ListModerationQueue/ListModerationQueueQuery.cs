namespace Application.Features.Administration.ContentModeration.ListModerationQueue;

/// <summary>Pede uma página da fila de moderação de conteúdo privado.</summary>
public sealed record ListModerationQueueQuery(
    ModerationContentKind Kind,
    ModerationStatusFilter Status = ModerationStatusFilter.All,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 50);
