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
    RemoveTrainingPlanStructure,
    /// <summary>Criação de sessão.</summary>
    CreateSession,
    /// <summary>Restaura uma sessão.</summary>
    RestoreSession,
    /// <summary>Reagendamento de uma sessão Scheduled.</summary>
    RescheduleSession,
    /// <summary>Criação de avaliação inicial.</summary>
    CreateInitialAssessment,
    /// <summary>Cria um check-in para o cliente do tenant.</summary>
    CreateCheckIn,
    /// <summary>Reagenda um check-in futuro sem respostas.</summary>
    RescheduleCheckIn
}
