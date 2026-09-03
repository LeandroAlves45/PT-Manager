namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Escreve os campos do perfil que o próprio cliente pode alterar.</summary>
public interface IMyProfileStore
{
    /// <summary>
    /// Aplica a atualização e devolve o resultado da escrita.
    /// </summary>
    Task<UpdateMyProfileOutcome> UpdateAsync(
        Guid trainerId,
        Guid clientUserId,
        UpdateMyProfileWriteModel writeModel,
        DateTime now,
        CancellationToken cancellationToken);
}
