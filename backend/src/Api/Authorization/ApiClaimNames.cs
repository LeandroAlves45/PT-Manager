namespace Api.Authorization;

/// <summary>Nomes estáveis das claims emitidas e validadas pela API.</summary>
public static class ApiClaimNames
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string TrainerId = "trainer_id";

    /// <summary>Momento do login que abriu a sessão (segundos Unix); não muda com o refresh.</summary>
    public const string AuthTime = "auth_time";
}
