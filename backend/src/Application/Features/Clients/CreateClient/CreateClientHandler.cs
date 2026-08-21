using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Clients;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Clients.CreateClient;

/// <summary>
/// Cria um cliente ativo e reserva automaticamente uma vaga na subscrição.
/// </summary>
public sealed class CreateClientHandler
{
    private readonly IValidator<CreateClientCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientStore _clientStore;

    /// <summary>Inicializa o caso de uso com as dependências necessárias.</summary>
    public CreateClientHandler(
        IValidator<CreateClientCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IClientStore clientStore)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
    }

    /// <summary>Valida, constrói e persiste cliente e contador numa transação.</summary>
    /// <param name="command">Perfil canónico do novo cliente.</param>
    /// <param name="cancellationToken">Sinal de cancelamento.</param>
    /// <returns>Detalhe criado ou falha esperada.</returns>
    public async Task<Result<ClientDetailsDto>> HandleAsync(
        CreateClientCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientDetailsDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientDetailsDto>.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var birthDate = BirthDate.Create(command.BirthDate, today);
        var sex = BiologicalSex.FromString(command.Sex);

        var client = new Client(
            actor.Value.TrainerId,
            command.Name,
            command.ContactEmail,
            command.Phone,
            birthDate,
            sex,
            command.Objective,
            command.Notes,
            command.EmergencyContactName,
            command.EmergencyContactPhone,
            now);

        var outcome = await _clientStore.CreateWithSubscriptionSlotAsync(
            client,
            actor.Value.TrainerId,
            now,
            cancellationToken);

        return outcome switch
        {
            CreateClientStoreOutcome.Created => Result<ClientDetailsDto>.Success(
                client.ToDetailsDto(new List<UsableClientPackDto>())),
            CreateClientStoreOutcome.DuplicateEmail => Result<ClientDetailsDto>.Failure(
                ClientErrors.ClientDuplicateEmail),
            CreateClientStoreOutcome.DuplicatePhone => Result<ClientDetailsDto>.Failure(
                ClientErrors.ClientDuplicatePhone),
            CreateClientStoreOutcome.SubscriptionInactive => Result<ClientDetailsDto>.Failure(
                ClientErrors.SubscriptionInactive),
            CreateClientStoreOutcome.SubscriptionSuspended => Result<ClientDetailsDto>.Failure(
                ClientErrors.SubscriptionSuspended),
            CreateClientStoreOutcome.SubscriptionCancelled => Result<ClientDetailsDto>.Failure(
                ClientErrors.SubscriptionCancelled),
            CreateClientStoreOutcome.ClientLimitReached => Result<ClientDetailsDto>.Failure(
                ClientErrors.ClientLimitReached),
            CreateClientStoreOutcome.SubscriptionMissing => throw new InvalidOperationException(
                "Trainer subscription required by signup was not found."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}
