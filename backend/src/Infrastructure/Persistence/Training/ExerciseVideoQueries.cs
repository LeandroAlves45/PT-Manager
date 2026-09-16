using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Media.Video;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Leituras de reprodução por audiência. Uma query por pedido, com o exercício e
/// o vídeo Ready resolvidos numa única junção.
/// </summary>
internal sealed class ExerciseVideoQueries : IExerciseVideoQueries
{
    private readonly PtManagerDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ExerciseVideoQueries> _logger;

    public ExerciseVideoQueries(
        PtManagerDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<ExerciseVideoQueries> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExerciseVideoPlaybackCandidate?> FindPlaybackCandidateAsync(
        ExerciseVideoPlaybackAudience audience,
        Guid exerciseId,
        Guid? trainerId,
        Guid? clientUserId,
        CancellationToken cancellationToken)
    {
        var (exercises, videos) = audience switch
        {
            ExerciseVideoPlaybackAudience.Trainer => ForTrainer(RequireId(trainerId)),
            ExerciseVideoPlaybackAudience.GlobalCatalog => ForGlobalCatalog(),
            ExerciseVideoPlaybackAudience.Administrative => ForAdministrative(),
            ExerciseVideoPlaybackAudience.Client => ForClient(RequireId(trainerId), RequireId(clientUserId)),
            _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, null)
        };

        var candidate = await exercises
            .Where(exercise => exercise.Id == exerciseId)
            .Join(
                videos.Where(video =>
                    video.ExerciseId == exerciseId &&
                    video.Status == ExerciseVideoStatus.Ready),
                exercise => exercise.Id,
                video => video.ExerciseId,
                (exercise, video) => new ExerciseVideoPlaybackCandidate(
                    video.Id,
                    video.ExerciseId,
                    video.OwnerTrainerId,
                    video.ObjectKey,
                    video.ContentType,
                    video.DurationMilliseconds!.Value,
                    video.Width!.Value,
                    video.Height!.Value,
                    exercise.PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked))
            .SingleOrDefaultAsync(cancellationToken);

        // Leitura administrativa de conteúdo potencialmente privado: sem auditoria
        // persistida, por decisão aprovada, mas com rasto estruturado sem PII.
        if (audience == ExerciseVideoPlaybackAudience.Administrative && candidate is not null)
            _logger.LogInformation(
                VideoLogEvents.AdministrativePlaybackIssued,
                "Administrative playback requested for exercise video {VideoId} of exercise " +
                "{ExerciseId} by user {ActorUserId}.",
                candidate.VideoId,
                candidate.ExerciseId,
                _tenantContext.UserId);

        return candidate;
    }

    /// <summary>Globais e privados do tenant, com os Global Query Filters ativos.</summary>
    private (IQueryable<Exercise>, IQueryable<ExerciseVideo>) ForTrainer(Guid trainerId) => (
        _dbContext.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.OwnerTrainerId == null || exercise.OwnerTrainerId == trainerId),
        _dbContext.ExerciseVideos
            .AsNoTracking()
            .Where(video => video.OwnerTrainerId == null || video.OwnerTrainerId == trainerId));

    /// <summary>Só o catálogo global; o superuser não tem tenant.</summary>
    private (IQueryable<Exercise>, IQueryable<ExerciseVideo>) ForGlobalCatalog() => (
        _dbContext.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(exercise => exercise.OwnerTrainerId == null),
        _dbContext.ExerciseVideos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(video => video.OwnerTrainerId == null));

    /// <summary>Qualquer exercício, só em contexto administrativo autorizado.</summary>
    private (IQueryable<Exercise>, IQueryable<ExerciseVideo>) ForAdministrative() => (
        _dbContext.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking(),
        _dbContext.ExerciseVideos
            .IgnoreQueryFilters()
            .AsNoTracking());

    /// <summary>
    /// Só exercícios referenciados pelo plano ativo do cliente autenticado. A
    /// definição de plano ativo é a mesma de <c>MyTrainingPlanQueries</c>.
    /// </summary>
    private (IQueryable<Exercise>, IQueryable<ExerciseVideo>) ForClient(
        Guid trainerId, Guid clientUserId) => (
            _dbContext.Exercises
                .AsNoTracking()
                .Where(exercise =>
                    (exercise.OwnerTrainerId == null || exercise.OwnerTrainerId == trainerId) &&
                    _dbContext.TrainingPlanDayExercises.Any(item =>
                        item.ExerciseId == exercise.Id &&
                        _dbContext.TrainingPlanDays.Any(day =>
                            day.Id == item.TrainingPlanDayId &&
                            _dbContext.TrainingPlans.Any(plan =>
                                plan.Id == day.TrainingPlanId &&
                                plan.OwnerTrainerId == trainerId &&
                                plan.IsActive &&
                                !plan.IsArchived &&
                                _dbContext.Clients.Any(client =>
                                    client.Id == plan.ClientId &&
                                    client.OwnerTrainerId == trainerId &&
                                    client.UserId == clientUserId &&
                                    client.IsActive))))),
            _dbContext.ExerciseVideos
                .AsNoTracking()
                .Where(video => video.OwnerTrainerId == null || video.OwnerTrainerId == trainerId));

    private static Guid RequireId(Guid? value) =>
        value is { } id && id != Guid.Empty
            ? id
            : throw new InvalidOperationException(
                "The playback audience requires a resolved identity.");
}
