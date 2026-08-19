namespace Application.Features.Supplements.ReactivateSupplement;

/// <summary>Identifica o suplemento privado a ser reativado.</summary>
public sealed record ReactivateSupplementCommand(Guid SupplementId);
