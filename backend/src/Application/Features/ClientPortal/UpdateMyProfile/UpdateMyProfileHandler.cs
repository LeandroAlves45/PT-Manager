using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.ClientPortal.UpdateMyProfile;

/// <summary>Atualiza o nome e os contactos do cliente autenticado.</summary>
public sealed class UpdateMyProfileHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IValidator<UpdateMyProfileCommand> _validator;
    private readonly IMyProfileStore _store;
    private readonly IClock _clock;

    public UpdateMyProfileHandler(
        ITenantContext tenantContext,
        IValidator<UpdateMyProfileCommand> validator,
        IMyProfileStore store,
        IClock clock)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<MyProfileDto>> HandleAsync(
        UpdateMyProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
            return Result<MyProfileDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyProfileDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            new UpdateMyProfileWriteModel(
                command.Name,
                command.Phone,
                command.EmergencyContactName,
                command.EmergencyContactPhone),
            _clock.UtcNow,
            cancellationToken);

        // O telefone é único dentro do tenant: sem este ramo a colisão sairia como 500.
        return outcome.Status switch
        {
            UpdateMyProfileStatus.Updated =>
                Result<MyProfileDto>.Success(outcome.Profile!),
            UpdateMyProfileStatus.DuplicatePhone =>
                Result<MyProfileDto>.Failure(ClientPortalErrors.ProfilePhoneAlreadyExists),
            _ => Result<MyProfileDto>.Failure(ClientPortalErrors.ProfileNotAvailable)
        };
    }
}
