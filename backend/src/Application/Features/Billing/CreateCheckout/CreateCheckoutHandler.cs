using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Billing.Abstractions;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Billing.CreateCheckout;

/// <summary>Orquestra Checkout sem conhecer o SDK externo.</summary>
public sealed class CreateCheckoutHandler
{
    private readonly IValidator<CreateCheckoutCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IBillingCheckoutStore _store;
    private readonly ICheckoutGateway _gateway;

    public CreateCheckoutHandler(
        IValidator<CreateCheckoutCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IBillingCheckoutStore store,
        ICheckoutGateway gateway
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public async Task<Result<Uri>> HandleAsync(
        CreateCheckoutCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<Uri>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext,
            BillingErrors.TrainerOnly
        );
        if (!actor.IsSuccess)
            return Result<Uri>.Failure(actor.Error!);

        var context = await _store.GetCheckoutContextAsync(
            actor.Value.TrainerId,
            cancellationToken
        );
        if (context is null)
            return Result<Uri>.Failure(BillingErrors.SubscriptionNotFound);

        var request = new CreateCheckoutRequest(
            actor.Value.TrainerId,
            command.OperationId,
            context.ProviderCustomerId,
            context.TrainerEmail,
            SubscriptionTier.FromString(command.Tier),
            command.SuccessUrl,
            command.CancelUrl,
            $"checkout:{actor.Value.TrainerId:N}:{command.OperationId:N}"
        );

        var checkout = await _gateway.CreateCheckoutAsync(request, cancellationToken);
        var linked = await _store.LinkCustomerAsync(
            actor.Value.TrainerId,
            checkout.ProviderCustomerId,
            _clock.UtcNow,
            cancellationToken
        );

        return linked.Kind switch
        {
            LinkPaymentCustomerStoreStatus.Linked or
            LinkPaymentCustomerStoreStatus.AlreadyLinkedToSameCustomer
                => Result<Uri>.Success(checkout.Url),
            LinkPaymentCustomerStoreStatus.SubscriptionNotFound =>
                Result<Uri>.Failure(BillingErrors.SubscriptionNotFound),
            LinkPaymentCustomerStoreStatus.LinkedToDifferentCustomer =>
                Result<Uri>.Failure(BillingErrors.CustomerConflict),
            LinkPaymentCustomerStoreStatus.ConcurrencyConflict =>
                Result<Uri>.Failure(BillingErrors.ConcurrencyConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(linked.Kind))
        };
    }
}
