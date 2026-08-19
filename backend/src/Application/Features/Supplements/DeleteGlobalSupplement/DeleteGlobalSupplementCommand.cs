namespace Application.Features.Supplements.DeleteGlobalSupplement;

/// <summary>Identifica suplemento global a ser eliminado fisicamente.</summary>
public sealed record DeleteGlobalSupplementCommand(Guid SupplementId);
