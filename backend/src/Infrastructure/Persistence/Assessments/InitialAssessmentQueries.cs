using Application.Features.Assessments;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Assessments;

/// <summary>Consultas InitialAssessment sem tracking.</summary>
internal sealed class InitialAssessmentQueries : IInitialAssessmentQueries
{
    private readonly PtManagerDbContext _dbContext;

    public InitialAssessmentQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<InitialAssessmentDto?> GetByClientAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken
    )
    {
        var assessment = await _dbContext.InitialAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OwnerTrainerId == trainerId &&
                    item.ClientId == clientId,
                cancellationToken);

        return assessment?.ToDto();
    }
}
