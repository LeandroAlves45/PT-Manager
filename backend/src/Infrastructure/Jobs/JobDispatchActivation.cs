using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.SessionReminders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jobs;

/// <summary>Ativa durable jobs e outbox sob o mesmo limite temporal.</summary>
internal sealed class JobDispatchActivation : IJobDispatchActivation
{
    private readonly ISessionReminderScanner _sessionReminderScanner;
    private readonly JobDispatcher _jobDispatcher;
    private readonly OutboxDispatcher _outboxDispatcher;
    private readonly JobDispatchOptions _options;
    private readonly ILogger<JobDispatchActivation> _logger;

    public JobDispatchActivation(
        ISessionReminderScanner sessionReminderScanner,
        JobDispatcher jobDispatcher,
        OutboxDispatcher outboxDispatcher,
        IOptions<JobDispatchOptions> options,
        ILogger<JobDispatchActivation> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _sessionReminderScanner = sessionReminderScanner ??
            throw new ArgumentNullException(nameof(sessionReminderScanner));
        _jobDispatcher = jobDispatcher ?? throw new ArgumentNullException(nameof(jobDispatcher));
        _outboxDispatcher = outboxDispatcher ?? throw new ArgumentNullException(nameof(outboxDispatcher));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            JobDispatchLogEvents.ActivationStarted,
            "Job dispatch activation started.");

        using var budgetSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetSource.CancelAfter(_options.ActivationTimeout);

        // Primeiro enfileira os lembretes da véspera, para a mesma ativação já os enviar.
        // Uma falha no scan não impede o resto do trabalho.
        try
        {
            await _sessionReminderScanner.ScanAsync(budgetSource.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            budgetSource.IsCancellationRequested)
        {
            _logger.LogWarning(
                JobDispatchLogEvents.ActivationFailure,
                "Session reminder scan reached the activation time budget.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                JobDispatchLogEvents.ActivationFailure,
                "Session reminder scan failed with failure type {FailureType}.",
                exception.GetType().Name);
        }

        try
        {
            await Task.WhenAll(
                _jobDispatcher.DispatchAsync(budgetSource.Token),
                _outboxDispatcher.DispatchAsync(budgetSource.Token));
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            budgetSource.IsCancellationRequested)
        {
            // O orçamento é uma condição operacional normal. Items em curso são
            // recuperados pelo lease e a próxima ativação continua o trabalho.
            _logger.LogWarning(
                JobDispatchLogEvents.ActivationFailure,
                "Job dispatch activation reached its configured time budget.");
        }
        catch (Exception exception)
        {
            // O recibo QStash já foi persistido. A próxima ativação agendada volta
            // a reclamar os itens, pelo que não se expõe o erro interno ao caller.
            _logger.LogError(
                JobDispatchLogEvents.ActivationFailure,
                "Job dispatch activation failed with failure type {FailureType}.",
                exception.GetType().Name);
        }
    }
}
