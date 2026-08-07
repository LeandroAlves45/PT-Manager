using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(clientStore);

        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _clientStore = clientStore;
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

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (tenant.IsFailure)
            return Result<ClientDetailsDto>.Failure(tenant.Error!);

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var birthDate = BirthDate.Create(command.BirthDate, today);
        var sex = BiologicalSex.FromString(command.Sex);

        var client = new Client(
            tenant.Value,
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
            tenant.Value,
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
