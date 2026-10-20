using Application.Common.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.TrainerInvitations;

public sealed record RegisterInvitedTrainerCommand(
    string? Token,
    string? FullName,
    string? Password,
    string? ConfirmPassword);

public sealed class RegisterInvitedTrainerCommandValidator
    : AbstractValidator<RegisterInvitedTrainerCommand>
{
    public RegisterInvitedTrainerCommandValidator()
    {
        RuleFor(command => command.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_invite_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_invite_token_invalid");

        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_full_name_required")
            .MaximumLength(255)
            .WithErrorCode("authentication_full_name_too_long")
            .WithoutNullCharacter();

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_password_required")
            .MinimumLength(8)
            .WithErrorCode("authentication_password_too_short")
            .MaximumLength(128)
            .WithErrorCode("authentication_password_too_long");

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.Password)
            .WithErrorCode("authentication_password_confirmation_mismatch")
            .WithMessage("Password confirmation must match the password.");
    }
}

/// <summary>
/// Cria a conta do treinador convidado com palavra-passe (sem Google, RN-008). A pessoa entra
/// a seguir pelo ecrã de entrar, como qualquer treinador.
/// </summary>
public sealed class RegisterInvitedTrainerHandler
{
    private readonly IValidator<RegisterInvitedTrainerCommand> _validator;
    private readonly ITrainerInvitationAcceptanceStore _store;
    private readonly IClock _clock;

    public RegisterInvitedTrainerHandler(
        IValidator<RegisterInvitedTrainerCommand> validator,
        ITrainerInvitationAcceptanceStore store,
        IClock clock)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> HandleAsync(
        RegisterInvitedTrainerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var status = await _store.RegisterAsync(
            command.Token!,
            command.FullName!.Trim(),
            command.Password!,
            _clock.UtcNow,
            cancellationToken);

        return status switch
        {
            TrainerInvitationStatus.Registered => Result.Success(),
            TrainerInvitationStatus.Invalid => Result.Failure(TrainerInvitationErrors.Invalid),
            TrainerInvitationStatus.Expired => Result.Failure(TrainerInvitationErrors.Expired),
            TrainerInvitationStatus.Used => Result.Failure(TrainerInvitationErrors.Used),
            // Um email que ganhou conta depois do envio: o convite já não pode servir, e dizer
            // porquê revelaria que a conta existe.
            TrainerInvitationStatus.AccountAlreadyExists =>
                Result.Failure(TrainerInvitationErrors.Invalid),
            TrainerInvitationStatus.PasswordRejected =>
                Result.Failure(AuthenticationErrors.RegistrationRejected()),
            TrainerInvitationStatus.ConcurrencyConflict =>
                Result.Failure(AuthenticationErrors.ConcurrencyConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }
}
