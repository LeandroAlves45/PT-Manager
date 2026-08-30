using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.BootstrapCsrf;

/// <summary>Roda o segredo anti-CSRF de uma sessão existente sem rodar o refresh token.</summary>
public sealed class BootstrapCsrfHandler
{
    private readonly IValidator<BootstrapCsrfCommand> _validator;
    private readonly IClock _clock;
    private readonly IAuthenticationSessionStore _store;

    public BootstrapCsrfHandler(
        IValidator<BootstrapCsrfCommand> validator,
        IClock clock,
        IAuthenticationSessionStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<CsrfTokenDto>> HandleAsync(
        BootstrapCsrfCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<CsrfTokenDto>.Failure(validation.ToApplicationError());

        var outcome = await _store.RotateCsrfAsync(
            command.RawToken,
            _clock.UtcNow,
            cancellationToken);

        if (outcome.Kind != RotateCsrfStoreStatus.Rotated)
        {
            var error = outcome.Kind switch
            {
                RotateCsrfStoreStatus.NotFound or
                RotateCsrfStoreStatus.Expired or
                RotateCsrfStoreStatus.Reused =>
                    AuthenticationErrors.RefreshSessionInvalid,
                RotateCsrfStoreStatus.ConcurrencyConflict =>
                    AuthenticationErrors.ConcurrencyConflict,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };

            return Result<CsrfTokenDto>.Failure(error);
        }

        if (outcome.RawCsrfToken is null)
            throw new InvalidOperationException(
                "The authentication session store returned Rotated without a CSRF token.");

        return Result<CsrfTokenDto>.Success(new(outcome.RawCsrfToken));
    }
}
