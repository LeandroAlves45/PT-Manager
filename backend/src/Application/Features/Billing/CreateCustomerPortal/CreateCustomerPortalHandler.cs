using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Billing.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Billing.CreateCustomerPortal;

/// <summary>Orquestra o portal sem conhecer SDK externo.</summary>
public sealed class CreateCustomerPortalHandler
{
    private readonly IValidator<CreateCustomerPortalCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IBillingCheckoutStore _store;
    private readonly ICustomerPortalGateway _gateway;

    public CreateCustomerPortalHandler(
        IValidator<CreateCustomerPortalCommand> validator,
        ITenantContext tenantContext,
        IBillingCheckoutStore store,
        ICustomerPortalGateway gateway)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public async Task<Result<Uri>> HandleAsync(
        CreateCustomerPortalCommand command,
        CancellationToken cancellationToken)
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

        var customerId = await _store.GetCustomerIdAsync(
            actor.Value.TrainerId,
            cancellationToken
        );
        if (customerId is null)
            return Result<Uri>.Failure(BillingErrors.CustomerNotLinked);

        var request = new CreateCustomerPortalRequest(
            actor.Value.TrainerId,
            command.OperationId,
            customerId,
            command.ReturnUrl,
            $"portal:{actor.Value.TrainerId:N}:{command.OperationId:N}"
        );

        var url = await _gateway.CreateCustomerPortalAsync(request, cancellationToken);
        return Result<Uri>.Success(url);
    }
}
