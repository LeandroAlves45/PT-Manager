namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados da persistência do signup.</summary>
public enum RegisterTrainerStoreStatus
{
    Created,
    DuplicateEmail,
    InvalidIdentityData,
    ConcurrencyConflict
}

/// <summary>Resultado do registo atómico de um personal trainer.</summary>
public sealed record RegisterTrainerStoreResult(
    RegisterTrainerStoreStatus Kind,
    Guid? UserId,
    Guid? TrainerId,
    IssuedAuthenticationSecret? EmailConfirmation)
{
    public static RegisterTrainerStoreResult Created(
        Guid userId,
        Guid trainerId,
        IssuedAuthenticationSecret emailConfirmation)
    {
        ArgumentNullException.ThrowIfNull(emailConfirmation);

        return new(
            RegisterTrainerStoreStatus.Created,
            userId,
            trainerId,
            emailConfirmation
        );
    }

    public static RegisterTrainerStoreResult For(RegisterTrainerStoreStatus status)
    {
        if (status == RegisterTrainerStoreStatus.Created)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null, null, null);
    }
}
