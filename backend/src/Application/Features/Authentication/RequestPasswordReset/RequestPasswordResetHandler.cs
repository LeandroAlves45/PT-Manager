using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.RequestPasswordReset;

/// <summary>Solicita reset sem revelar se a conta existe.</summary>
public sealed class RequestPasswordResetHandler
{
    private readonly IValidator<RequestPasswordResetCommand> _validator;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IPasswordResetRequestStore _store;
    private readonly IAuthenticationEmailSender _emailSender;

    public RequestPasswordResetHandler(
        IValidator<RequestPasswordResetCommand> validator,
        IClock clock,
        AuthenticationPolicy policy,
        IPasswordResetRequestStore store,
        IAuthenticationEmailSender emailSender)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    public async Task<Result> HandleAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var outcome = await _store.IssueAsync(
            command.Email.Trim(),
            now.Add(_policy.PasswordResetLifetime),
            now,
            cancellationToken
        );

        if (outcome.Kind == PasswordResetRequestStoreStatus.NotEligible)
            return Result.Success();
        if (outcome.Kind == PasswordResetRequestStoreStatus.ConcurrencyConflict)
            return Result.Failure(AuthenticationErrors.ConcurrencyConflict);
        if (outcome.Kind != PasswordResetRequestStoreStatus.Issued)
            throw new ArgumentOutOfRangeException(nameof(outcome.Kind));

        await _emailSender.SendPasswordResetAsync(
            outcome.Secret!,
            cancellationToken
        );

        // A resposta pública permanece igual mesmo quando a entrega falha,
        // evitando que a disponibilidade do email revele a existência da conta.
        return Result.Success();
    }
}
