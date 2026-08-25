using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.ChangePassword;

/// <summary>Orquestra mudança, stamp e revogação sem exigir tenant.</summary>
public sealed class ChangePasswordHandler
{
    private readonly IValidator<ChangePasswordCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPasswordManagementStore _store;

    public ChangePasswordHandler(
        IValidator<ChangePasswordCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IPasswordManagementStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        if (!_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            _tenantContext.Role is not ("trainer" or "client" or "superuser"))
            return Result.Failure(AuthenticationErrors.AuthenticatedAccountRequired);

        var outcome = await _store.ChangeAsync(
            _tenantContext.UserId.Value,
            command.CurrentPassword,
            command.NewPassword,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.Kind switch
        {
            PasswordManagementStoreStatus.Changed => Result.Success(),
            PasswordManagementStoreStatus.UserNotFound =>
                Result.Failure(AuthenticationErrors.AuthenticatedAccountRequired),
            PasswordManagementStoreStatus.CurrentPasswordInvalid =>
                Result.Failure(AuthenticationErrors.CurrentPasswordInvalid),
            PasswordManagementStoreStatus.NewPasswordInvalid =>
                Result.Failure(AuthenticationErrors.NewPasswordRejected()),
            PasswordManagementStoreStatus.ConcurrencyConflict =>
                Result.Failure(AuthenticationErrors.ConcurrencyConflict),
            PasswordManagementStoreStatus.ResetTokenNotFound or
            PasswordManagementStoreStatus.ResetTokenExpired or
            PasswordManagementStoreStatus.ResetTokenConsumed =>
                throw new InvalidOperationException(
                    "A change password store returned a reset only outcome."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
