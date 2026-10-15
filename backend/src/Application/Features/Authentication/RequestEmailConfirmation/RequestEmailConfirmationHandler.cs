using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.RequestEmailConfirmation;

/// <summary>Reenvia a confirmação de email sem revelar se a conta existe.</summary>
public sealed class RequestEmailConfirmationHandler
{
    private readonly IValidator<RequestEmailConfirmationCommand> _validator;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IEmailConfirmationStore _store;
    private readonly IAuthenticationEmailSender _emailSender;

    /// <summary>Cria o handler com as dependências do reenvio por email.</summary>
    public RequestEmailConfirmationHandler(
        IValidator<RequestEmailConfirmationCommand> validator,
        IClock clock,
        AuthenticationPolicy policy,
        IEmailConfirmationStore store,
        IAuthenticationEmailSender emailSender)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    /// <summary>
    /// Só o formato do email pode falhar. Conta inexistente, inativa, já confirmada, conflito
    /// de concorrência e falha de entrega dão todos sucesso: qualquer diferença na resposta
    /// revelaria que o email tem conta.
    /// </summary>
    public async Task<Result> HandleAsync(
        RequestEmailConfirmationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var outcome = await _store.IssueByEmailAsync(
            command.Email.Trim(),
            now.Add(_policy.EmailConfirmationLifetime),
            now,
            cancellationToken);

        if (outcome.Kind == EmailConfirmationStoreStatus.Issued)
            await _emailSender.SendEmailConfirmationAsync(outcome.Secret!, cancellationToken);

        return Result.Success();
    }
}
