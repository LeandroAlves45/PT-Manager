using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.RegisterTrainer;

/// <summary>Regista um personal trainer e entrega a confirmação apenas depois do commit.</summary>
public sealed class RegisterTrainerHandler
{
    private readonly IValidator<RegisterTrainerCommand> _validator;
    private readonly IClock _clock;
    private readonly AuthenticationPolicy _policy;
    private readonly IAuthenticationRegistrationStore _store;
    private readonly IAuthenticationEmailSender _emailSender;

    public RegisterTrainerHandler(
        IValidator<RegisterTrainerCommand> validator,
        IClock clock,
        AuthenticationPolicy policy,
        IAuthenticationRegistrationStore store,
        IAuthenticationEmailSender emailSender)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    /// <summary>
    /// Cria o registo de um personal trainer e envia a confirmação sem compensação destrutiva.
    /// </summary>
    public async Task<Result<RegisteredTrainerDto>> HandleAsync(
        RegisterTrainerCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<RegisteredTrainerDto>.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var request = new RegisterTrainerStoreRequest(
            command.Email.Trim(),
            command.Password,
            command.FullName.Trim(),
            now.AddDays(_policy.TrialDays),
            now.Add(_policy.EmailConfirmationLifetime)
        );

        var outcome = await _store.RegisterTrainerAsync(request, cancellationToken);
        if (outcome.Kind != RegisterTrainerStoreStatus.Created)
        {
            return outcome.Kind switch
            {
                RegisterTrainerStoreStatus.DuplicateEmail =>
                    Result<RegisteredTrainerDto>.Failure(
                        AuthenticationErrors.DuplicateEmail),
                RegisterTrainerStoreStatus.InvalidIdentityData =>
                    Result<RegisteredTrainerDto>.Failure(
                        AuthenticationErrors.RegistrationRejected()),
                RegisterTrainerStoreStatus.ConcurrencyConflict =>
                    Result<RegisteredTrainerDto>.Failure(
                        AuthenticationErrors.ConcurrencyConflict),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
            };
        }

        var delivery = await _emailSender.SendEmailConfirmationAsync(
            outcome.EmailConfirmation!,
            cancellationToken
        );

        if (delivery == AuthenticationEmailDeliveryOutcome.Unavailable)
            return Result<RegisteredTrainerDto>.Failure(
                AuthenticationErrors.EmailDeliveryUnavailable);

        return Result<RegisteredTrainerDto>.Success(new RegisteredTrainerDto(
            outcome.UserId!.Value,
            outcome.TrainerId!.Value,
            request.Email,
            request.TrialEndsAt
        ));
    }
}
