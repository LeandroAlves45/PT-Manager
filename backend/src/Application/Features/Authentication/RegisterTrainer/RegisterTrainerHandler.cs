using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.RegisterTrainer;

/// <summary>
/// Regista um personal trainer sem revelar se o email já tinha conta: o resultado é o mesmo
/// e é o email enviado que difere.
/// </summary>
public sealed class RegisterTrainerHandler
{
    private readonly IValidator<RegisterTrainerCommand> _validator;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IAuthenticationRegistrationStore _store;
    private readonly IEmailConfirmationStore _confirmations;
    private readonly IAuthenticationEmailSender _emailSender;

    public RegisterTrainerHandler(
        IValidator<RegisterTrainerCommand> validator,
        IClock clock,
        AuthenticationPolicy policy,
        IAuthenticationRegistrationStore store,
        IEmailConfirmationStore confirmations,
        IAuthenticationEmailSender emailSender)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _confirmations = confirmations ?? throw new ArgumentNullException(nameof(confirmations));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    /// <summary>
    /// Cria a conta e envia a confirmação sem compensação destrutiva. Quando o email já
    /// existe, reenvia a confirmação (conta por confirmar) ou avisa o dono (conta confirmada).
    /// A falha de entrega nunca altera a resposta: a conta já existe e o pedido de
    /// reenvio recupera.
    /// </summary>
    public async Task<Result> HandleAsync(
        RegisterTrainerCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var email = command.Email.Trim();
        var request = new RegisterTrainerStoreRequest(
            email,
            command.Password,
            command.FullName.Trim(),
            now.AddDays(_policy.TrialDays),
            now.Add(_policy.EmailConfirmationLifetime)
        );

        var outcome = await _store.RegisterTrainerAsync(request, cancellationToken);
        switch (outcome.Kind)
        {
            case RegisterTrainerStoreStatus.Created:
                await _emailSender.SendEmailConfirmationAsync(
                    outcome.EmailConfirmation!,
                    cancellationToken);
                return Result.Success();
            case RegisterTrainerStoreStatus.DuplicateEmail:
                await NotifyExistingAccountAsync(email, now, cancellationToken);
                return Result.Success();
            case RegisterTrainerStoreStatus.InvalidIdentityData:
                return Result.Failure(AuthenticationErrors.RegistrationRejected());
            case RegisterTrainerStoreStatus.ConcurrencyConflict:
                return Result.Failure(AuthenticationErrors.ConcurrencyConflict);
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome.Kind));
        }
    }

    private async Task NotifyExistingAccountAsync(
        string email,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var issued = await _confirmations.IssueByEmailAsync(
            email,
            now.Add(_policy.EmailConfirmationLifetime),
            now,
            cancellationToken);

        // Conta inativa, desaparecida ou em corrida: não há nada útil a enviar.
        switch (issued.Kind)
        {
            case EmailConfirmationStoreStatus.Issued:
                await _emailSender.SendEmailConfirmationAsync(issued.Secret!, cancellationToken);
                break;
            case EmailConfirmationStoreStatus.AlreadyConfirmed:
                await _emailSender.SendAccountAlreadyExistsAsync(email, cancellationToken);
                break;
        }
    }
}
