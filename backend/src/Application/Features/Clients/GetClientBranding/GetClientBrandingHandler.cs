using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Results;

namespace Application.Features.Clients.GetClientBranding;

/// <summary>
/// Obtém o branding do portal para o cliente autenticado e ativo. Cliente
/// inexistente, arquivado ou sem TrainerSettings devolvem o mesmo NotFound,
/// sem revelar qual condição falhou.
/// </summary>
public sealed class GetClientBrandingHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClientBrandingQueries _queries;

    public GetClientBrandingHandler(
        ITenantContext tenantContext,
        IClientBrandingQueries queries
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<ClientBrandingDto>> HandleAsync(
        CancellationToken cancellationToken
    )
    {
        var actor = ActorAuthorization.RequireClient(
            _tenantContext, TrainerSettings.TrainerSettingsErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<ClientBrandingDto>.Failure(actor.Error!);

        var branding = await _queries.GetAsync(
            actor.Value.TrainerId, actor.Value.UserId, cancellationToken);

        return branding is null
            ? Result<ClientBrandingDto>.Failure(
                TrainerSettings.TrainerSettingsErrors.BrandingNotAvailable)
            : Result<ClientBrandingDto>.Success(branding);
    }
}
