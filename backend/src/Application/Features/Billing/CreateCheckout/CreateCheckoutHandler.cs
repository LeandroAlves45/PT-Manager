using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Billing.Abstractions;
using Application.Errors;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Billing.CreateCheckout;

/// <summary>Coordena intenção local, customer e Checkout sem transação durante I/O externo.</summary>
public sealed class CreateCheckoutHandler
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
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

        var reservation = await _store.ReserveAsync(
            actor.Value.TrainerId,
            command.OperationId,
            SubscriptionTier.FromString(command.Tier),
            _clock.UtcNow,
            LeaseDuration,
            cancellationToken);

        if (reservation.Status == CheckoutReservationStatus.ResumeCreated)
        {
            var recovered = await _gateway.GetSessionAsync(
                reservation.ProviderSessionId!,
                cancellationToken);
            return MapSession(recovered);
        }
        if (reservation.Status != CheckoutReservationStatus.Acquired)
            return Result<Uri>.Failure(MapReservationError(reservation.Status));

        var customerId = reservation.ProviderCustomerId;
        if (customerId is null)
        {
            var customer = await _gateway.EnsureCustomerAsync(
                new EnsureCustomerRequest(
                    actor.Value.TrainerId,
                    reservation.TrainerEmail!,
                    $"billing:customer:{actor.Value.TrainerId:N}"),
                cancellationToken);
            if (customer.Status != BillingGatewayStatus.Success)
            {
                await _store.MarkFailedAsync(
                    reservation.OperationId,
                    reservation.LeaseOwnerId,
                    FailureCode(customer.Status),
                    _clock.UtcNow,
                    cancellationToken);
                return Result<Uri>.Failure(MapGatewayError(customer.Status));
            }
            customerId = customer.ProviderCustomerId;
            var linked = await _store.LinkCustomerAsync(
                reservation.OperationId,
                reservation.LeaseOwnerId,
                customerId!,
                _clock.UtcNow,
                cancellationToken);
            if (linked is not CheckoutMutationStatus.Applied and not CheckoutMutationStatus.AlreadyApplied)
                return Result<Uri>.Failure(MapMutationError(linked));
        }

        var checkout = await _gateway.CreateSessionAsync(
            new CreateCheckoutRequest(
                actor.Value.TrainerId,
                command.OperationId,
                customerId!,
                reservation.Tier!,
                reservation.EffectiveTrialEndsAt,
                $"billing:checkout:{command.OperationId:N}"),
            cancellationToken);
        if (checkout.Status != BillingGatewayStatus.Success)
        {
            await _store.MarkFailedAsync(
                reservation.OperationId,
                reservation.LeaseOwnerId,
                FailureCode(checkout.Status),
                _clock.UtcNow,
                cancellationToken);
            return Result<Uri>.Failure(MapGatewayError(checkout.Status));
        }

        var completed = await _store.MarkSessionCreatedAsync(
            reservation.OperationId,
            reservation.LeaseOwnerId,
            checkout.ProviderSessionId!,
            checkout.ExpiresAt!.Value,
            _clock.UtcNow,
            cancellationToken);
        if (completed is not CheckoutMutationStatus.Applied and not CheckoutMutationStatus.AlreadyApplied)
            return Result<Uri>.Failure(MapMutationError(completed));

        return Result<Uri>.Success(checkout.Url!);
    }

    private static Result<Uri> MapSession(CheckoutSessionOutcome outcome) =>
        outcome.Status == BillingGatewayStatus.Success
            ? Result<Uri>.Success(outcome.Url!)
            : Result<Uri>.Failure(MapGatewayError(outcome.Status));

    private static Error MapReservationError(
        CheckoutReservationStatus status) =>
        status switch
        {
            CheckoutReservationStatus.SubscriptionNotFound => BillingErrors.SubscriptionNotFound,
            CheckoutReservationStatus.SameKeyDifferentTier => BillingErrors.OperationTierConflict,
            CheckoutReservationStatus.AnotherOperationActive => BillingErrors.ActiveCheckoutExists,
            CheckoutReservationStatus.AlreadySubscribed => BillingErrors.UseCustomerPortal,
            CheckoutReservationStatus.BillingExempt => BillingErrors.CheckoutNotAvailable,
            _ => BillingErrors.ConcurrencyConflict
        };

    private static Error MapMutationError(
        CheckoutMutationStatus status) =>
        status switch
        {
            CheckoutMutationStatus.CustomerConflict => BillingErrors.CustomerConflict,
            CheckoutMutationStatus.LeaseLost => BillingErrors.CheckoutLeaseLost,
            CheckoutMutationStatus.NotFound => BillingErrors.SubscriptionNotFound,
            _ => BillingErrors.ConcurrencyConflict
        };

    private static Error MapGatewayError(BillingGatewayStatus status) =>
        status switch
        {
            BillingGatewayStatus.Disabled => BillingErrors.StripeDisabled,
            BillingGatewayStatus.ConfigurationMismatch => BillingErrors.ProviderConfigurationMismatch,
            BillingGatewayStatus.InvalidResponse => BillingErrors.ProviderInvalidResponse,
            BillingGatewayStatus.NotFound => BillingErrors.ProviderInvalidResponse,
            _ => BillingErrors.ProviderUnavailable
        };

    private static string FailureCode(BillingGatewayStatus status) => status switch
    {
        BillingGatewayStatus.Disabled => "provider_disabled",
        BillingGatewayStatus.ConfigurationMismatch => "configuration_mismatch",
        BillingGatewayStatus.InvalidResponse => "invalid_response",
        BillingGatewayStatus.NotFound => "not_found",
        _ => "transient_failure"
    };
}
