using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Observability;
using Application.Features.ClientPortal.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.ClientPortal.DeleteMyAccount;

/// <summary>
/// O cliente elimina a própria conta: o acesso e os dados que o identificam desaparecem e o
/// histórico fica no tenant do personal trainer, sem identificação.
/// </summary>
public sealed class DeleteMyAccountHandler
{
    private readonly IValidator<DeleteMyAccountCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyAccountDeletionStore _store;

    public DeleteMyAccountHandler(
        IValidator<DeleteMyAccountCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IMyAccountDeletionStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        DeleteMyAccountCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var status = await _store.DeleteAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            string.IsNullOrEmpty(command.Password) ? null : command.Password,
            OperationCorrelation.NewId(),
            _clock.UtcNow,
            cancellationToken);

        return status switch
        {
            MyAccountDeletionStatus.Deleted => Result.Success(),
            MyAccountDeletionStatus.NotFound => Result.Failure(ClientPortalErrors.ProfileNotAvailable),
            MyAccountDeletionStatus.PasswordRequired or MyAccountDeletionStatus.PasswordInvalid =>
                Result.Failure(ClientPortalErrors.AccountDeletionPasswordInvalid),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
}
