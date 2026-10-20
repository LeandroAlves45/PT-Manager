using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Features.Billing.Abstractions;
using Application.Results;

namespace Application.Features.Administration.Trainers.SetBillingExemption;

public sealed record SetBillingExemptionCommand(Guid TrainerId, bool IsExempt);

/// <summary>
/// Dá ou tira a isenção de faturação (RN-008, U42, U43). Isentar cancela primeiro no Stripe a
/// subscrição que ainda cobra: se a app gravasse antes e o Stripe falhasse, o treinador ficava
/// isento e continuava a pagar. Repetir o pedido é seguro nos dois sentidos.
/// </summary>
public sealed class SetBillingExemptionHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationQueries _queries;
    private readonly ITrainerAdministrationStore _store;
    private readonly ISubscriptionCancellationGateway _cancellation;
    private readonly IClock _clock;

    public SetBillingExemptionHandler(
        ITenantContext tenantContext,
        ITrainerAdministrationQueries queries,
        ITrainerAdministrationStore store,
        ISubscriptionCancellationGateway cancellation,
        IClock clock)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<AdminTrainerDto>> HandleAsync(
        SetBillingExemptionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<AdminTrainerDto>.Failure(actor.Error!);

        if (command.IsExempt)
        {
            var chargeable = await _queries.FindChargeableStripeSubscriptionAsync(
                command.TrainerId,
                cancellationToken);
            if (chargeable is not null &&
                await _cancellation.CancelImmediatelyAsync(chargeable, cancellationToken) !=
                    BillingGatewayStatus.Success)
                return Result<AdminTrainerDto>.Failure(
                    TrainerAdministrationErrors.PaymentProviderUnavailable);
        }

        var status = await _store.SetBillingExemptionAsync(
            actor.Value.UserId,
            command.TrainerId,
            command.IsExempt,
            _clock.UtcNow,
            cancellationToken);

        var failure = status switch
        {
            TrainerAdministrationStoreStatus.Changed or
            TrainerAdministrationStoreStatus.Unchanged => null,
            TrainerAdministrationStoreStatus.NotFound => TrainerAdministrationErrors.TrainerNotFound,
            TrainerAdministrationStoreStatus.ActorInvalid => TrainerAdministrationErrors.AdministratorOnly,
            TrainerAdministrationStoreStatus.ConcurrencyConflict =>
                TrainerAdministrationErrors.ConcurrencyConflict,
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        if (failure is not null)
            return Result<AdminTrainerDto>.Failure(failure);

        var trainer = await _queries.GetTrainerAsync(command.TrainerId, cancellationToken);
        return trainer is null
            ? Result<AdminTrainerDto>.Failure(TrainerAdministrationErrors.TrainerNotFound)
            : Result<AdminTrainerDto>.Success(trainer);
    }
}
