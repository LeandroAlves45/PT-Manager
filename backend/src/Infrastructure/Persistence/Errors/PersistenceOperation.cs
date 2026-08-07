namespace Infrastructure.Persistence.Errors;

/// <summary>
/// Contexto mínimo necessário para interpretar uma constraint sem depender
/// apenas do SQLSTATE.
/// </summary>
internal enum PersistenceOperation
{
    /// <summary>Criação de cliente.</summary>
    CreateClient,
    /// <summary>Atualização do perfil de cliente.</summary>
    UpdateClient,
    /// <summary>Remoção estrutural de um plano de treino.</summary>
    RemoveTrainingPlanStructure
}
