using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal;
using Application.Results;

namespace Application.Features.Training.ExerciseVideos;

/// <summary>
/// Resolve, de forma fail-closed, o ator autorizado e o owner efetivo de cada
/// operação. O owner nunca vem do route, da query ou do body.
/// </summary>
internal static class ExerciseVideoActors
{
    /// <summary>Owner nulo para o catálogo global; tenant efetivo para o privado.</summary>
    internal static Result<WriterActor> ResolveWriter(
        ITenantContext tenantContext,
        ExerciseVideoCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        switch (catalog)
        {
            case ExerciseVideoCatalog.Private:
                var trainer = ActorAuthorization.RequireTrainer(
                    tenantContext,
                    TrainingErrors.TrainerOnly);
                return trainer.IsSuccess
                    ? Result<WriterActor>.Success(
                        new WriterActor(
                            catalog,
                            trainer.Value.TrainerId,
                            trainer.Value.UserId))
                    : Result<WriterActor>.Failure(trainer.Error!);

            case ExerciseVideoCatalog.Global:
                var admin = ActorAuthorization.RequireAdministrator(
                    tenantContext,
                    TrainingErrors.AdministratorOnly);
                return admin.IsSuccess
                    ? Result<WriterActor>.Success(
                        new WriterActor(
                            catalog,
                            null,
                            admin.Value.UserId))
                    : Result<WriterActor>.Failure(admin.Error!);

            default:
                throw new ArgumentOutOfRangeException(nameof(catalog), catalog, null);
        }
    }

    /// <summary>Resolve tenant e utilizador exigidos pela audiência de reprodução.</summary>
    internal static Result<ReaderActor> ResolveReader(
        ITenantContext tenantContext,
        ExerciseVideoPlaybackAudience audience)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        switch (audience)
        {
            case ExerciseVideoPlaybackAudience.Trainer:
                var trainer = ActorAuthorization.RequireTrainer(
                    tenantContext,
                    TrainingErrors.TrainerOnly);
                return trainer.IsSuccess
                    ? Result<ReaderActor>.Success(new ReaderActor(trainer.Value.TrainerId, null))
                    : Result<ReaderActor>.Failure(trainer.Error!);

            case ExerciseVideoPlaybackAudience.GlobalCatalog:
            case ExerciseVideoPlaybackAudience.Administrative:
                var admin = ActorAuthorization.RequireAdministrator(
                    tenantContext,
                    TrainingErrors.AdministratorOnly);
                return admin.IsSuccess
                    ? Result<ReaderActor>.Success(new ReaderActor(null, null))
                    : Result<ReaderActor>.Failure(admin.Error!);

            case ExerciseVideoPlaybackAudience.Client:
                var client = ActorAuthorization.RequireClient(
                    tenantContext,
                    ClientPortalErrors.ClientOnly);
                return client.IsSuccess
                    ? Result<ReaderActor>.Success(
                        new ReaderActor(client.Value.TrainerId, client.Value.UserId))
                    : Result<ReaderActor>.Failure(client.Error!);

            default:
                throw new ArgumentOutOfRangeException(nameof(audience), audience, null);
        }
    }

    internal sealed record WriterActor(
        ExerciseVideoCatalog Catalog,
        Guid? OwnerTrainerId,
        Guid UserId);

    internal sealed record ReaderActor(Guid? TrainerId, Guid? ClientUserId);
}
