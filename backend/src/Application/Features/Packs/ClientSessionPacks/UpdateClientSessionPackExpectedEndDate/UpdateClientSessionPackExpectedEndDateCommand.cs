namespace Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate;

/// <summary>Altera ou remove a data esperada de um pack atribuído.</summary>
public sealed record UpdateClientSessionPackExpectedEndDateCommand(
    Guid ClientSessionPackId,
    DateOnly? ExpectedEndDate
);
