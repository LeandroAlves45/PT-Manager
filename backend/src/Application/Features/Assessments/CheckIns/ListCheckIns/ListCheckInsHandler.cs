using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Assessments.CheckIns.ListCheckIns;

/// <summary>Lista Check-ins do personal trainer autenticado.</summary>
public sealed class ListCheckInsHandler
{
    private readonly IValidator<ListCheckInsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInQueries _queries;

    public ListCheckInsHandler(
        IValidator<ListCheckInsQuery> validator,
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInQueries queries
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timeZoneProvider);
        ArgumentNullException.ThrowIfNull(queries);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _queries = queries;
    }

    public async Task<Result<PageResult<CheckInDto>>> HandleAsync(
        ListCheckInsQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<CheckInDto>>.Failure(validation.ToApplicationError());

        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<PageResult<CheckInDto>>.Failure(tenant.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(tenant.Value, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, timeZone));

        var page = await _queries.ListAsync(
            tenant.Value,
            query.ClientId,
            query.Status,
            query.FromDate,
            query.ToDate,
            today,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<CheckInDto>>.Success(page);
    }
}
