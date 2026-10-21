using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Results;

namespace Application.Features.Clients.GetClientInvitation;

/// <summary>
/// Diz ao trainer se o convite da ficha está por aceitar, expirou ou já foi aceite (U63, §4.5).
/// Ficha inexistente ou de outro tenant devolve NotFound, como o detalhe.
/// </summary>
public sealed class GetClientInvitationHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientQueries _clientQueries;

    public GetClientInvitationHandler(
        ITenantContext tenantContext,
        IClock clock,
        IClientQueries clientQueries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _clientQueries = clientQueries ?? throw new ArgumentNullException(nameof(clientQueries));
    }

    public async Task<Result<ClientInvitationDto>> HandleAsync(
        GetClientInvitationQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ClientId == Guid.Empty)
            return Result<ClientInvitationDto>.Failure(ClientErrors.ClientIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientInvitationDto>.Failure(actor.Error!);

        var snapshot = await _clientQueries.GetInvitationAsync(
            actor.Value.TrainerId,
            query.ClientId,
            cancellationToken);
        if (snapshot is null)
            return Result<ClientInvitationDto>.Failure(ClientErrors.ClientNotFound);

        if (snapshot.HasAccount)
            return Result<ClientInvitationDto>.Success(
                new ClientInvitationDto(ClientInvitationStates.Accepted, null, null));
        if (snapshot.PendingExpiresAt is not { } expiresAt)
            return Result<ClientInvitationDto>.Success(
                new ClientInvitationDto(ClientInvitationStates.None, null, null));

        // Igual a InviteToken.IsValid: só é válido enquanto a validade está no futuro.
        var state = expiresAt > _clock.UtcNow
            ? ClientInvitationStates.Pending
            : ClientInvitationStates.Expired;
        return Result<ClientInvitationDto>.Success(
            new ClientInvitationDto(state, snapshot.PendingEmail, expiresAt));
    }
}
