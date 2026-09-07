namespace Application.Features.Jobs.Dispatching;

/// <summary>Autentica e consome de forma idempotente um pedido interno assinado.</summary>
public interface IInternalDispatchRequestAuthenticator
{
    Task<InternalDispatchAuthenticationResult> AuthenticateAsync(
        string signature,
        ReadOnlyMemory<byte> rawBody,
        CancellationToken cancellationToken
    );
}
