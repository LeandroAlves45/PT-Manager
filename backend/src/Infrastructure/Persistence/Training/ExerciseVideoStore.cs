using System.Data;
using System.Text.Json;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Jobs;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Escritas HTTP de vídeos geridos. Cada mutação grava estado, durable jobs e,
/// no catálogo global, a auditoria administrativa na mesma transação.
/// </summary>
internal sealed class ExerciseVideoStore : IExerciseVideoStore
{
    private const string AuditResourceType = "exercise_video";

    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _translator;

    public ExerciseVideoStore(
        PtManagerDbContext dbContext,
        PostgresConstraintTranslator translator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public async Task<ExerciseVideoRegistrationStatus> RegisterUploadAsync(
        ExerciseVideoRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        EnsureCatalogOwner(registration.Catalog, registration.Video.OwnerTrainerId);

        var videoId = registration.Video.Id;
        try
        {
            return await ExecuteAsync(
                token => RegisterOnceAsync(registration, token),
                token => _dbContext.ExerciseVideos
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(video => video.Id == videoId, token),
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _translator.TryTranslate(
                exception,
                PersistenceOperation.RegisterExerciseVideoUpload,
                out var error) &&
            error?.Code == ExerciseVideoErrors.UploadInProgress.Code)
        {
            // O índice parcial é a autoridade quando dois pedidos passam a
            // verificação em simultâneo.
            return ExerciseVideoRegistrationStatus.UploadInProgress;
        }
    }

    public Task<ExerciseVideo?> FindUploadAsync(
        ExerciseVideoCatalog catalog,
        Guid exerciseId,
        Guid videoId,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken)
    {
        EnsureCatalogOwner(catalog, ownerTrainerId);

        // Filtros ignorados com predicado explícito: o superuser não tem tenant e o
        // Global Query Filter esconderia todas as linhas.
        return _dbContext.ExerciseVideos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(video =>
                video.Id == videoId &&
                video.ExerciseId == exerciseId &&
                video.OwnerTrainerId == ownerTrainerId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<ExerciseVideoUploadTransition> MarkUploadedAsync(
        ExerciseVideoUploadCompletion completion,
        long storedSizeBytes,
        string storedETag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);
        EnsureCatalogOwner(completion.Catalog, completion.OwnerTrainerId);

        var processKey = ExerciseVideoJobs.ProcessIdempotencyKey(completion.VideoId);
        return ExecuteAsync(
            async token =>
            {
                var video = await _dbContext.LockVideoAsync(
                    completion.VideoId,
                    completion.OwnerTrainerId,
                    token);
                if (video is null || video.ExerciseId != completion.ExerciseId)
                    return new ExerciseVideoUploadTransition(
                        ExerciseVideoUploadTransitionStatus.NotFound);

                if (video.Status == ExerciseVideoStatus.Processing ||
                    video.Status == ExerciseVideoStatus.Ready)
                    return new ExerciseVideoUploadTransition(
                        ExerciseVideoUploadTransitionStatus.AlreadyApplied, video);

                if (video.Status != ExerciseVideoStatus.Pending || storedSizeBytes != video.DeclaredSizeBytes)
                    return new ExerciseVideoUploadTransition(
                        ExerciseVideoUploadTransitionStatus.InvalidState, video);

                if (!video.IsUploadWindowOpen(completion.Now))
                    return new ExerciseVideoUploadTransition(
                        ExerciseVideoUploadTransitionStatus.UploadWindowClosed, video);

                var before = Snapshot(video);
                video.MarkUploaded(storedSizeBytes, storedETag, completion.Now);

                _dbContext.DurableJobs.Add(new DurableJob(
                    video.OwnerTrainerId,
                    ExerciseVideoJobs.ProcessType,
                    ExerciseVideoJobs.Version,
                    ExerciseVideoJobs.SerializeVideoPayload(video.Id),
                    processKey,
                    completion.CorrelationId,
                    completion.Now,
                    completion.Now));

                AddAuditWhenGlobal(
                    completion.Catalog,
                    completion.ActorUserId,
                    "complete_upload",
                    video,
                    before,
                    completion.Now);

                await _dbContext.SaveChangesAsync(token);
                return new ExerciseVideoUploadTransition(
                    ExerciseVideoUploadTransitionStatus.Applied, video);
            },
            token => JobExistsAsync(processKey, token),
            cancellationToken);
    }

    public Task<ExerciseVideoUploadTransition> RejectUploadAsync(
        ExerciseVideoUploadCompletion completion,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);
        EnsureCatalogOwner(completion.Catalog, completion.OwnerTrainerId);

        var deleteKey = ExerciseVideoJobs.DeleteObjectIdempotencyKey(completion.VideoId);
        return ExecuteAsync(
            async token =>
            {
                var video = await _dbContext.LockVideoAsync(
                    completion.VideoId,
                    completion.OwnerTrainerId,
                    token);
                if (video is null || video.ExerciseId != completion.ExerciseId)
                    return new ExerciseVideoUploadTransition(
                        ExerciseVideoUploadTransitionStatus.NotFound);

                if (video.Status == ExerciseVideoStatus.Rejected ||
                    video.Status == ExerciseVideoStatus.Failed)
                    return new ExerciseVideoUploadTransition(
                        ExerciseVideoUploadTransitionStatus.AlreadyApplied, video);

                if (video.Status != ExerciseVideoStatus.Pending)
                    return new ExerciseVideoUploadTransition(
                        ExerciseVideoUploadTransitionStatus.InvalidState, video);

                var before = Snapshot(video);
                video.Reject(failureCode, completion.Now);
                AddObjectDeletion(video, completion.CorrelationId, completion.Now);
                AddAuditWhenGlobal(
                    completion.Catalog,
                    completion.ActorUserId,
                    "reject_upload",
                    video,
                    before,
                    completion.Now);

                await _dbContext.SaveChangesAsync(token);
                return new ExerciseVideoUploadTransition(
                    ExerciseVideoUploadTransitionStatus.Applied, video);
            },
            token => JobExistsAsync(deleteKey, token),
            cancellationToken);
    }

    public Task<ExerciseVideoRemovalStatus> RemoveReadyAsync(
        ExerciseVideoCatalog catalog,
        Guid exerciseId,
        Guid? ownerTrainerId,
        Guid actorUserId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        EnsureCatalogOwner(catalog, ownerTrainerId);

        Guid? removedVideoId = null;
        return ExecuteAsync(
            async token =>
            {
                removedVideoId = null;
                var video = await _dbContext.LockReadyVideoAsync(
                    exerciseId,
                    ownerTrainerId,
                    token);
                if (video is null)
                    return ExerciseVideoRemovalStatus.NotFound;

                removedVideoId = video.Id;
                var before = Snapshot(video);
                _dbContext.ExerciseVideos.Remove(video);
                AddObjectDeletion(video, correlationId, now);

                if (catalog == ExerciseVideoCatalog.Global)
                    _dbContext.AdministrativeAuditEntries.Add(new AdministrativeAuditEntry(
                        actorUserId,
                        "remove",
                        AuditResourceType,
                        video.Id,
                        before,
                        null,
                        now));

                await _dbContext.SaveChangesAsync(token);
                return ExerciseVideoRemovalStatus.Removed;
            },
            token => removedVideoId is { } id
                ? JobExistsAsync(ExerciseVideoJobs.DeleteObjectIdempotencyKey(id), token)
                : Task.FromResult(false),
            cancellationToken);
    }

    private async Task<ExerciseVideoRegistrationStatus> RegisterOnceAsync(
        ExerciseVideoRegistration registration,
        CancellationToken cancellationToken)
    {
        var video = registration.Video;
        var exercise = registration.Catalog == ExerciseVideoCatalog.Private
            ? await _dbContext.LockPrivateExerciseAsync(
                video.ExerciseId,
                video.OwnerTrainerId!.Value,
                cancellationToken)
            : await _dbContext.LockGlobalExerciseAsync(
                video.ExerciseId,
                cancellationToken);

        if (exercise is null)
            return ExerciseVideoRegistrationStatus.ExerciseNotFound;
        if (!exercise.IsActive)
            return ExerciseVideoRegistrationStatus.ExerciseInactive;
        if (exercise.PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked)
            return ExerciseVideoRegistrationStatus.ExerciseBlocked;

        var exerciseVideos = _dbContext.ExerciseVideos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(candidate => candidate.ExerciseId == video.ExerciseId);

        if (await exerciseVideos.AnyAsync(
            candidate =>
                candidate.Status == ExerciseVideoStatus.Pending ||
                candidate.Status == ExerciseVideoStatus.Processing,
            cancellationToken))
            return ExerciseVideoRegistrationStatus.UploadInProgress;

        if (video.OwnerTrainerId is { } trainerId &&
            await IsQuotaExceededAsync(
                trainerId, video.ExerciseId, registration.MaxVideosPerTrainer, cancellationToken))
            return ExerciseVideoRegistrationStatus.QuotaExceeded;

        _dbContext.ExerciseVideos.Add(video);
        _dbContext.DurableJobs.Add(new DurableJob(
            video.OwnerTrainerId,
            ExerciseVideoJobs.ExpireType,
            ExerciseVideoJobs.Version,
            ExerciseVideoJobs.SerializeVideoPayload(video.Id),
            ExerciseVideoJobs.ExpireIdempotencyKey(video.Id),
            registration.CorrelationId,
            registration.CleanupScheduledAt,
            registration.Now));

        if (registration.Catalog == ExerciseVideoCatalog.Global)
            _dbContext.AdministrativeAuditEntries.Add(new AdministrativeAuditEntry(
                registration.ActorUserId,
                "request_upload",
                AuditResourceType,
                video.Id,
                null,
                Snapshot(video),
                registration.Now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ExerciseVideoRegistrationStatus.Registered;
    }

    /// <summary>
    /// A quota conta exercícios com vídeo publicado ou em curso, não linhas: a
    /// substituição do vídeo de um exercício já contado nunca exige uma vaga extra.
    /// </summary>
    private async Task<bool> IsQuotaExceededAsync(
        Guid trainerId,
        Guid exerciseId,
        int maxVideosPerTrainer,
        CancellationToken cancellationToken)
    {
        await _dbContext.AcquireVideoQuotaLockAsync(trainerId, cancellationToken);

        var counted = _dbContext.ExerciseVideos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(candidate =>
                candidate.OwnerTrainerId == trainerId &&
                (candidate.Status == ExerciseVideoStatus.Pending ||
                    candidate.Status == ExerciseVideoStatus.Processing ||
                    candidate.Status == ExerciseVideoStatus.Ready));

        if (await counted.AnyAsync(candidate => candidate.ExerciseId == exerciseId, cancellationToken))
            return false;

        var usedExercises = await counted
            .Select(candidate => candidate.ExerciseId)
            .Distinct()
            .CountAsync(cancellationToken);

        return usedExercises >= maxVideosPerTrainer;
    }

    private void AddObjectDeletion(ExerciseVideo video, Guid correlationId, DateTime now) =>
        _dbContext.DurableJobs.Add(new DurableJob(
            video.OwnerTrainerId,
            ExerciseVideoJobs.DeleteObjectType,
            ExerciseVideoJobs.Version,
            ExerciseVideoJobs.SerializeDeleteObjectPayload(video.Id, video.ObjectKey),
            ExerciseVideoJobs.DeleteObjectIdempotencyKey(video.Id),
            correlationId,
            now,
            now));

    private void AddAuditWhenGlobal(
        ExerciseVideoCatalog catalog,
        Guid actorUserId,
        string action,
        ExerciseVideo video,
        string before,
        DateTime now)
    {
        if (catalog != ExerciseVideoCatalog.Global)
            return;

        _dbContext.AdministrativeAuditEntries.Add(new AdministrativeAuditEntry(
            actorUserId,
            action,
            AuditResourceType,
            video.Id,
            before,
            Snapshot(video),
            now));
    }

    private Task<bool> JobExistsAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        _dbContext.DurableJobs
            .AsNoTracking()
            .AnyAsync(job => job.IdempotencyKey == idempotencyKey, cancellationToken);

    private Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded,
        CancellationToken cancellationToken)
    {
        // Uma tentativa repetida reconstrói o tracking a partir da DB.
        Func<CancellationToken, Task<T>> attempt = async operationToken =>
        {
            _dbContext.ChangeTracker.Clear();
            return await operation(operationToken);
        };

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            attempt,
            verifySucceeded,
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    private static void EnsureCatalogOwner(ExerciseVideoCatalog catalog, Guid? ownerTrainerId)
    {
        // Um caller interno que misture catálogo e owner viola a trust boundary;
        // não é uma falha funcional devolvida ao utilizador.
        var consistent = catalog switch
        {
            ExerciseVideoCatalog.Private => ownerTrainerId is { } id && id != Guid.Empty,
            ExerciseVideoCatalog.Global => ownerTrainerId is null,
            _ => false
        };

        if (!consistent)
            throw new InvalidOperationException("Exercise video catalog does not match the owner.");
    }

    // O identificador do objeto fica fora da auditoria: é derivável e não
    // acrescenta prova sobre a decisão do ator.
    private static string Snapshot(ExerciseVideo video) => JsonSerializer.Serialize(new
    {
        id = video.Id,
        exercise_id = video.ExerciseId,
        status = video.Status.Value,
        content_type = video.ContentType,
        declared_size_bytes = video.DeclaredSizeBytes,
        stored_size_bytes = video.StoredSizeBytes,
        failure_code = video.FailureCode,
        updated_at = video.UpdatedAt
    });
}

