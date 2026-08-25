using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.ConfirmEmail;

/// <summary>Confirma o email atrvés de consumo concorrente do token.</summary>
public sealed class ConfirmEmailHandler
{
    private readonly IValidator<ConfirmEmailCommand> _validator;
    private readonly IClock _clock;
    private readonly IEmailConfirmationStore _store;

    public ConfirmEmailHandler(
        IValidator<ConfirmEmailCommand> validator,
        IClock clock,
        IEmailConfirmationStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ConfirmEmailCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var outcome = await _store.ConsumeAsync(command.Token, _clock.UtcNow, cancellationToken);

        return outcome.Kind switch
        {
            EmailConfirmationStoreStatus.Confirmed or
            EmailConfirmationStoreStatus.AlreadyConfirmed => Result.Success(),
            EmailConfirmationStoreStatus.TokenNotFound =>
                Result.Failure(AuthenticationErrors.ConfirmationTokenInvalid),
            EmailConfirmationStoreStatus.TokenExpired =>
                Result.Failure(AuthenticationErrors.ConfirmationTokenExpired),
            EmailConfirmationStoreStatus.TokenAlreadyConsumed =>
                Result.Failure(AuthenticationErrors.ConfirmationTokenConsumed),
            EmailConfirmationStoreStatus.AccountInactive =>
                Result.Failure(AuthenticationErrors.AccountInactive),
            EmailConfirmationStoreStatus.ConcurrencyConflict =>
                Result.Failure(AuthenticationErrors.ConcurrencyConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
