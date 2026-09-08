namespace Api.Configuration;

/// <summary>Nomes estáveis das políticas do endpoint interno.</summary>
public static class InternalJobDispatchPolicyNames
{
    /// <summary>Limite por IP aplicado antes da autenticação QStash.</summary>
    public const string Dispatch = "internal_job_dispatch";
}
