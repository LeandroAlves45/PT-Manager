using Application.Common.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Authentication.TrainerInvitations;

public sealed record GetTrainerInvitationStatusQuery(string? Token);

public sealed class GetTrainerInvitationStatusQueryValidator
    : AbstractValidator<GetTrainerInvitationStatusQuery>
{
    public GetTrainerInvitationStatusQueryValidator()
    {
        RuleFor(query => query.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_invite_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_invite_token_invalid");
    }
}

/// <summary>Diz à página do convite em que estado está o link antes de pedir dados.</summary>
public sealed class GetTrainerInvitationStatusHandler
{
    private readonly IValidator<GetTrainerInvitationStatusQuery> _validator;
    private readonly ITrainerInvitationAcceptanceStore _store;
    private readonly IClock _clock;

    public GetTrainerInvitationStatusHandler(
        IValidator<GetTrainerInvitationStatusQuery> validator,
        ITrainerInvitationAcceptanceStore store,
        IClock clock)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<TrainerInvitationStatusResult>> HandleAsync(
        GetTrainerInvitationStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainerInvitationStatusResult>.Failure(validation.ToApplicationError());

        return Result<TrainerInvitationStatusResult>.Success(
            await _store.GetStatusAsync(query.Token!, _clock.UtcNow, cancellationToken));
    }
}
