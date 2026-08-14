namespace Application.Features.Packs.PackTypes.ReactivatePackType;

/// <summary>Solicita a reativação de um tipo de pack privado.</summary>
public sealed record ReactivatePackTypeCommand(Guid PackTypeId);
