namespace Application.Features.Administration.ContentModeration.ListModerationQueue;

/// <summary>Catálogo privado listado na fila de moderação.</summary>
public enum ModerationContentKind
{
    Food,
    Exercise
}

/// <summary>
/// Estados de enforcement pedido. Valores de uma só palavra porque a query string liga
/// enums por nome (case-insensitive) e não pela forma snake_case do JSON.
/// </summary>
public enum ModerationStatusFilter
{
    All,
    Allowed,
    Blocked
}
