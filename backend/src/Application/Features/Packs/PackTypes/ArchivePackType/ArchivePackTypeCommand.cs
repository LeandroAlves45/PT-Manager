namespace Application.Features.Packs.PackTypes.ArchivePackType;

/// <summary>Solicita a arquivação de um tipo de pack privado.</summary>
public sealed record ArchivePackTypeCommand(Guid PackTypeId);
