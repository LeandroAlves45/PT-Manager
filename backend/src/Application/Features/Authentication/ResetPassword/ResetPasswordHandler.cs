using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.ResetPassword;

/// <summary>Orquestra reset sem conhecer Identity ou EF Core.</summary>
public sealed class ResetPasswordHandler
{
    private readonly IValidator<ResetPasswordCommand> _validator;
    private readonly IClock _clock;
    private readonly IPasswordManagementStore _store;

    public ResetPasswordHandler(
        IValidator<ResetPasswordCommand> validator,
        IClock clock,
        IPasswordManagementStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var outcome = await _store.ResetAsync(
            command.Token,
            command.NewPassword,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.Kind switch
        {
            PasswordManagementStoreStatus.Changed => Result.Success(),
            PasswordManagementStoreStatus.ResetTokenNotFound or
            PasswordManagementStoreStatus.ResetTokenExpired or
            PasswordManagementStoreStatus.ResetTokenConsumed or
            PasswordManagementStoreStatus.UserNotFound =>
                Result.Failure(AuthenticationErrors.PasswordResetInvalid),
            PasswordManagementStoreStatus.NewPasswordInvalid =>
                Result.Failure(AuthenticationErrors.NewPasswordRejected()),
            PasswordManagementStoreStatus.ConcurrencyConflict =>
                Result.Failure(AuthenticationErrors.ConcurrencyConflict),
            PasswordManagementStoreStatus.CurrentPasswordInvalid =>
                throw new InvalidOperationException(
                    "A reset password store returned a change-only outcome."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
