using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.Logout;

/// <summary>Orquestra revogação sem revelar o estado anterior do token.</summary>
public sealed class LogoutHandler
{
    private readonly IValidator<LogoutCommand> _validator;
    private readonly IClock _clock;
    private readonly IAuthenticationSessionStore _store;

    public LogoutHandler(
        IValidator<LogoutCommand> validator,
        IClock clock,
        IAuthenticationSessionStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var status = await _store.RevokeAsync(
            command.RawToken,
            command.RawCsrfToken,
            _clock.UtcNow,
            cancellationToken);

        return status == RevokeSessionStoreStatus.CsrfInvalid
            ? Result.Failure(AuthenticationErrors.CsrfTokenInvalid)
            : Result.Success();
    }
}
