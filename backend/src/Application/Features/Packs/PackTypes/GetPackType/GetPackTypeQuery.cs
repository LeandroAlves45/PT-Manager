namespace Application.Features.Packs.PackTypes.GetPackType;

/// <summary>Solicita um tipo de pack pelo identificador.</summary>
public sealed record GetPackTypeQuery(Guid PackTypeId);
