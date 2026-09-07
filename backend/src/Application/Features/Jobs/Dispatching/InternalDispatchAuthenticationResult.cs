namespace Application.Features.Jobs.Dispatching;

/// <summary>Resultado fechado da autenticação de uma ativação interna.</summary>
public enum InternalDispatchAuthenticationStatus
{
    Accepted,
    Replay,
    Invalid,
    Unavailable
}

/// <summary>Resultado da fronteira de autenticação do ativador.</summary>
public sealed record InternalDispatchAuthenticationResult(
    InternalDispatchAuthenticationStatus Status)
{
    public static InternalDispatchAuthenticationResult Accepted() =>
        new(InternalDispatchAuthenticationStatus.Accepted);

    public static InternalDispatchAuthenticationResult Replay() =>
        new(InternalDispatchAuthenticationStatus.Replay);

    public static InternalDispatchAuthenticationResult Invalid() =>
        new(InternalDispatchAuthenticationStatus.Invalid);

    public static InternalDispatchAuthenticationResult Unavailable() =>
        new(InternalDispatchAuthenticationStatus.Unavailable);
}
