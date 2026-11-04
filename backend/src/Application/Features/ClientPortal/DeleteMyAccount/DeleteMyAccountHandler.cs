using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Observability;
using Application.Features.ClientPortal.Abstractions;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.ClientPortal.DeleteMyAccount;

/// <summary>
/// O cliente elimina a própria conta: o acesso e os dados que o identificam desaparecem e o
/// histórico fica no tenant do personal trainer, sem identificação.
/// </summary>
public sealed class DeleteMyAccountHandler
{
    /// <summary>Janela de "login recente" para eliminar uma conta sem password (só Google).</summary>
    public static readonly TimeSpan RecentSignInWindow = TimeSpan.FromMinutes(10);

    private readonly IValidator<DeleteMyAccountCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyAccountDeletionStore _store;

    public DeleteMyAccountHandler(
        IValidator<DeleteMyAccountCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IMyAccountDeletionStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        DeleteMyAccountCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToApplicationError());

        var now = _clock.UtcNow;
        var status = await _store.DeleteAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            string.IsNullOrEmpty(command.Password) ? null : command.Password,
            IsRecentSignIn(command.AuthenticatedAt, now),
            OperationCorrelation.NewId(),
            now,
            cancellationToken);

        return status switch
        {
            MyAccountDeletionStatus.Deleted => Result.Success(),
            MyAccountDeletionStatus.NotFound => Result.Failure(ClientPortalErrors.ProfileNotAvailable),
            MyAccountDeletionStatus.PasswordRequired or MyAccountDeletionStatus.PasswordInvalid =>
                Result.Failure(ClientPortalErrors.AccountDeletionPasswordInvalid),
            MyAccountDeletionStatus.ReauthenticationRequired =>
                Result.Failure(ClientPortalErrors.AccountDeletionReauthenticationRequired),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    /// <summary>
    /// Sem password, quem tem um access token roubado (até 15 min) eliminaria a conta só com a
    /// palavra. Exigir um login recente obriga a passar outra vez pelo Google. Um momento no
    /// futuro não conta como recente.
    /// </summary>
    private static bool IsRecentSignIn(DateTime? authenticatedAt, DateTime now) =>
        authenticatedAt is { } signedInAt && signedInAt <= now && now - signedInAt <= RecentSignInWindow;
}
