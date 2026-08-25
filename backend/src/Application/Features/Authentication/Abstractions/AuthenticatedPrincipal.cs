namespace Application.Features.Authentication.Abstractions;

/// <summary>Identidade validada pelo store antes da emissão de um access token.</summary>
public sealed record AuthenticatedPrincipal
{
    public Guid UserId { get; }
    public Guid? TrainerId { get; }
    public string Role { get; }
    public string SecurityStamp { get; }

    /// <summary>Valida o contrato devolvido pela Infrastructure.</summary>
    public AuthenticatedPrincipal(
        Guid userId,
        Guid? trainerId,
        string role,
        string securityStamp)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User Id is required.", nameof(userId));

        if (role is not ("trainer" or "client" or "superuser"))
            throw new ArgumentException("Role is invalid.", nameof(role));

        if (role == "superuser" && trainerId.HasValue)
            throw new ArgumentException("A superuser cannot enter a tenant.", nameof(trainerId));

        if (role != "superuser" && (!trainerId.HasValue || trainerId.Value == Guid.Empty))
            throw new ArgumentException("A tenant role requires a trainer Id.", nameof(trainerId));

        if (string.IsNullOrWhiteSpace(securityStamp))
            throw new ArgumentException("Security stamp is required.", nameof(securityStamp));

        UserId = userId;
        TrainerId = trainerId;
        Role = role;
        SecurityStamp = securityStamp;
    }
}
