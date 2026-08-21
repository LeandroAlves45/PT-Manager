using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Clients.UpdateClient;

/// <summary> Atualiza um cliente ativo sem alterar o estado. </summary>
public sealed class UpdateClientHandler
{
    private readonly IValidator<UpdateClientCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientStore _clientStore;
    private readonly IClientQueries _clientQueries;

    /// <summary>Inicializa as dependências de validação, escrita e projeção. </summary>
    public UpdateClientHandler(
        IValidator<UpdateClientCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IClientStore clientStore,
        IClientQueries clientQueries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
        _clientQueries = clientQueries ?? throw new ArgumentNullException(nameof(clientQueries));
    }

    /// <summary>Aplica o perfil completo através do Domain e devolve o detalhe atual.</summary>
    public async Task<Result<ClientDetailsDto>> HandleAsync(
        UpdateClientCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientDetailsDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientDetailsDto>.Failure(actor.Error!);

        var client = await _clientStore.GetForUpdateAsync(command.ClientId, cancellationToken);
        if (client is null)
            return Result<ClientDetailsDto>.Failure(ClientErrors.ClientNotFound);

        var now = _clock.UtcNow;
        client.UpdateProfile(
            command.Name,
            command.ContactEmail,
            command.Phone,
            BirthDate.Create(command.BirthDate, DateOnly.FromDateTime(now)),
            BiologicalSex.FromString(command.Sex),
            command.Objective,
            command.Notes,
            command.EmergencyContactName,
            command.EmergencyContactPhone,
            now
        );

        var outcome = await _clientStore.SaveProfileAsync(client, cancellationToken);
        if (outcome == SaveClientProfileOutcome.DuplicateEmail)
            return Result<ClientDetailsDto>.Failure(ClientErrors.ClientDuplicateEmail);
        if (outcome == SaveClientProfileOutcome.DuplicatePhone)
            return Result<ClientDetailsDto>.Failure(ClientErrors.ClientDuplicatePhone);
        if (outcome != SaveClientProfileOutcome.Updated)
            throw new ArgumentOutOfRangeException(nameof(outcome));

        var packs = await _clientQueries.ListUsablePacksAsync(
            client.Id,
            cancellationToken);
        return Result<ClientDetailsDto>.Success(client.ToDetailsDto(packs));
    }
}
