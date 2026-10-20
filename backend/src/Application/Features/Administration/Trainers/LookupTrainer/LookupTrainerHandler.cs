using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Results;
using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Administration.Trainers.LookupTrainer;

public sealed record LookupTrainerQuery(string? Email);

public sealed class LookupTrainerQueryValidator : AbstractValidator<LookupTrainerQuery>
{
    public LookupTrainerQueryValidator()
    {
        RuleFor(query => query.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("admin_trainer_email_required")
            .Must(EmailAddress.IsValid)
            .WithErrorCode("admin_trainer_email_invalid");
    }
}

/// <summary>Procura um treinador pelo email completo; nunca por prefixo (RN-018).</summary>
public sealed class LookupTrainerHandler
{
    private readonly IValidator<LookupTrainerQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationQueries _queries;

    public LookupTrainerHandler(
        IValidator<LookupTrainerQuery> validator,
        ITenantContext tenantContext,
        ITrainerAdministrationQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<TrainerLookupDto>> HandleAsync(
        LookupTrainerQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainerLookupDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<TrainerLookupDto>.Failure(actor.Error!);

        var email = new EmailAddress(query.Email!);
        return Result<TrainerLookupDto>.Success(
            await _queries.LookupByEmailAsync(email.Normalized, cancellationToken));
    }
}
