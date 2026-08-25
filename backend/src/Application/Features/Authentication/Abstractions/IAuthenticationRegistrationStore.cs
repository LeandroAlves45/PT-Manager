namespace Application.Features.Authentication.Abstractions;

/// <summary>Persiste o signup completo numa única transação PostgreSQL.</summary>
public interface IAuthenticationRegistrationStore
{
    Task<RegisterTrainerStoreResult> RegisterTrainerAsync(
        RegisterTrainerStoreRequest request,
        CancellationToken cancellationToken);
}
