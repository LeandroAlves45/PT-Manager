using Application.Features.Assessments;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Pagination;
using Domain.Entities.Assessments;
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

    public async Task<bool?> IsPendingForClientUserAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken)
    {
        var client = await _dbContext.Clients
            .AsNoTracking()
            .Where(client => client.OwnerTrainerId == trainerId &&
                client.UserId == clientUserId &&
                client.IsActive)
            .Select(client => new
            {
                HasAssessment = _dbContext.InitialAssessments.Any(assessment =>
                    assessment.OwnerTrainerId == trainerId &&
                    assessment.ClientId == client.Id &&
                    !assessment.IsDeleted)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return client is null ? null : !client.HasAssessment;
    }

    public async Task<PageResult<InitialAssessmentPendingReviewDto>> ListPendingReviewAsync(
        Guid trainerId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pending = PendingReview(_dbContext, trainerId);
        var totalCount = await pending.CountAsync(cancellationToken);
        var items = await ToPendingReviewItems(
                _dbContext,
                pending
                    .OrderBy(assessment => assessment.CreatedAt)
                    .ThenBy(assessment => assessment.Id)
                    .Skip((page.PageNumber - 1) * page.PageSize)
                    .Take(page.PageSize))
            .ToListAsync(cancellationToken);

        return new PageResult<InitialAssessmentPendingReviewDto>(items, totalCount);
    }

    /// <summary>
    /// Avaliações gravadas pelo cliente e ainda por rever, só de clientes ativos: o arquivado
    /// sai da lista e volta se for reativado. O painel compõe esta mesma consulta. Filtra e
    /// ordena-se sobre a entidade; a projeção vem no fim.
    /// </summary>
    internal static IQueryable<InitialAssessment> PendingReview(
        PtManagerDbContext dbContext,
        Guid trainerId) =>
        dbContext.InitialAssessments
            .AsNoTracking()
            .Where(assessment => assessment.OwnerTrainerId == trainerId &&
                !assessment.IsDeleted &&
                assessment.SubmittedByClient &&
                assessment.ReviewedAt == null &&
                dbContext.Clients.Any(client =>
                    client.Id == assessment.ClientId &&
                    client.OwnerTrainerId == trainerId &&
                    client.IsActive));

    /// <summary>
    /// Projeta linhas já filtradas, ordenadas e paginadas, com o nome do cliente numa
    /// subconsulta (sem junção, a ordem da página mantém-se).
    /// </summary>
    internal static IQueryable<InitialAssessmentPendingReviewDto> ToPendingReviewItems(
        PtManagerDbContext dbContext,
        IQueryable<InitialAssessment> assessments) =>
        assessments.Select(assessment => new InitialAssessmentPendingReviewDto(
            assessment.Id,
            assessment.ClientId,
            dbContext.Clients
                .Where(client => client.Id == assessment.ClientId)
                .Select(client => client.Name)
                .First(),
            assessment.CreatedAt));
}
