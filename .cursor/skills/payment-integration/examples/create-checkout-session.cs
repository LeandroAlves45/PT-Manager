// create-checkout-session.cs
// Espelha o handler real: Application/Features/Billing/CreateCheckout/CreateCheckoutHandler.cs
// Cria uma Stripe Checkout Session para subscrição, sem o handler conhecer o SDK Stripe.

using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Billing.Abstractions;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Billing.CreateCheckout;

/// Comando de entrada
public sealed record CreateCheckoutCommand(
    Guid OperationId,
    string Tier,
    Uri SuccessUrl,
    Uri CancelUrl);

/// Handler real (Application layer, gateway-agnostic)
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
        ICheckoutGateway gateway)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public async Task<Result<Uri>> HandleAsync(
        CreateCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Validação de entrada
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<Uri>.Failure(validation.ToApplicationError());

        // 2. Só trainers criam checkout (subscrição é do trainer, não do cliente)
        var actor = ActorAuthorization.RequireTrainer(_tenantContext, BillingErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<Uri>.Failure(actor.Error!);

        // 3. Obter contexto de checkout (customer Stripe já ligado, se existir)
        var context = await _store.GetCheckoutContextAsync(actor.Value.TrainerId, cancellationToken);
        if (context is null)
            return Result<Uri>.Failure(BillingErrors.SubscriptionNotFound);

        // 4. Idempotency key derivada do domínio, não gerada à parte
        var idempotencyKey = $"checkout:{actor.Value.TrainerId:N}:{command.OperationId:N}";

        var request = new CreateCheckoutRequest(
            actor.Value.TrainerId,
            command.OperationId,
            context.ProviderCustomerId,
            context.TrainerEmail,
            SubscriptionTier.FromString(command.Tier),
            command.SuccessUrl,
            command.CancelUrl,
            idempotencyKey);

        // 5. Gateway isola o SDK Stripe — handler nunca vê PaymentIntent/PaymentMethod
        var checkout = await _gateway.CreateCheckoutAsync(request, cancellationToken);

        // 6. Ligar o customer Stripe à subscrição do trainer (idempotente)
        var linked = await _store.LinkCustomerAsync(
            actor.Value.TrainerId,
            checkout.ProviderCustomerId,
            _clock.UtcNow,
            cancellationToken);

        // 7. Só devolvemos a URL de redirect — nunca card data, nunca client secret de PaymentIntent
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

/// Gateway a implementar na Infrastructure — única fronteira com o SDK Stripe.net
public interface ICheckoutGateway
{
    Task<CheckoutResult> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct);
}

public sealed record CheckoutResult(Uri Url, string ProviderCustomerId);

/// Regras:
/// 1. Handler nunca importa `Stripe` (namespace do SDK) — só fala com ICheckoutGateway
/// 2. Idempotency key = identidade de domínio (trainer + operation), não um GUID solto
/// 3. Resposta ao cliente é só a Url de redirect — nunca client secret, nunca card data
/// 4. Autorização: RequireTrainer — subscrição pertence ao trainer, não ao cliente final
/// 5. Erros mapeados via Result<T>, nunca exception para fluxo esperado (ex.: customer conflict)
