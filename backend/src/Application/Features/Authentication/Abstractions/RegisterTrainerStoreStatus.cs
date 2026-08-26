namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados da persistência do signup.</summary>
public enum RegisterTrainerStoreStatus
{
    Created,
    DuplicateEmail,
    InvalidIdentityData,
    ConcurrencyConflict
}
