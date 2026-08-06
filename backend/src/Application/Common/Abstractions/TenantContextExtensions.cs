using Application.Errors;
using Application.Results;

namespace Application.Common.Abstractions;

/// <summary>
/// Fornece operações fail-closed sobre o contexto tenant já validado.
/// </summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// Obtêm o tenant necessário para um caso de uso tenant-owned.
    /// Nunca interpreta tenant ausente como acesso global.
    /// </summary>
    public static Result<Guid> GetRequiredTrainerId(this ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext, nameof(tenantContext));

        if (!tenantContext.TrainerId.HasValue || tenantContext.TrainerId.Value == Guid.Empty)
            return Result<Guid>.Failure(CommonErrors.TenantRequired);

        return Result<Guid>.Success(tenantContext.TrainerId.Value);
    }
}
