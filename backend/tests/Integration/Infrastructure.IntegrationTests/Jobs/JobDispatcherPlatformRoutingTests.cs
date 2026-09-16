using Application.Common.Abstractions;
using Application.Features.Jobs.Abstractions;
using Application.Features.Jobs.Dispatching;
using Domain.Entities.Jobs;
using Infrastructure.Data;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Prova o caminho sem tenant do dispatcher: só um handler de plataforma executa
/// um job com TrainerId nulo, com origem System e sem contexto administrativo.
/// Qualquer outro handler continua a ser recusado antes de executar.
/// </summary>
public sealed class JobDispatcherPlatformRoutingTests
{
    private static readonly DateTime Now = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task JobWithoutTenant_RunsThePlatformHandlerWithSystemOrigin()
    {
        var store = new ScriptedJobStore(Claimed("platform.test"));
        var recorder = new ContextRecorder();

        await CreateDispatcher(store, recorder).DispatchAsync(Token);

        Assert.Equal(1, recorder.PlatformCalls);
        Assert.Equal((null, TenantOrigin.System, false), (recorder.TrainerId, recorder.Origin, recorder.IsAdministrative));
        Assert.Equal(1, store.Completed);
        Assert.Null(store.FailureCode);
    }

    [Fact]
    public async Task JobWithoutTenant_ForATenantHandler_FailsPermanentlyWithoutRunningIt()
    {
        var store = new ScriptedJobStore(Claimed("tenant.test"));
        var recorder = new ContextRecorder();

        await CreateDispatcher(store, recorder).DispatchAsync(Token);

        Assert.Equal(0, recorder.TenantCalls);
        Assert.Equal(("job_tenant_unavailable", (DateTime?)null), (store.FailureCode, store.NextAttemptAt));
        Assert.Equal(0, store.Completed);
    }

    private static DurableJob Claimed(string jobType)
    {
        var job = new DurableJob(null, jobType, 1, "{}", Guid.NewGuid().ToString("N"), Guid.NewGuid(), Now, Now);
        job.Claim(Guid.NewGuid(), TimeSpan.FromMinutes(1), Now);
        return job;
    }

    private static JobDispatcher CreateDispatcher(ScriptedJobStore store, ContextRecorder recorder)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDurableJobStore>(store);
        services.AddSingleton(recorder);
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<IDurableJobHandler, PlatformHandler>();
        services.AddScoped<IDurableJobHandler, TenantHandler>();

        var provider = services.BuildServiceProvider();
        return new JobDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new JobDispatchOptions()),
            new TestClock(Now),
            NullLogger<JobDispatcher>.Instance);
    }

    private sealed class ContextRecorder
    {
        public int PlatformCalls { get; set; }
        public int TenantCalls { get; set; }
        public Guid? TrainerId { get; set; }
        public TenantOrigin? Origin { get; set; }
        public bool? IsAdministrative { get; set; }
    }

    private sealed class PlatformHandler(ContextRecorder recorder, ITenantContext tenantContext) : IPlatformDurableJobHandler
    {
        public string JobType => "platform.test";
        public int JobVersion => 1;

        public Task<DispatchItemOutcome> HandleAsync(DurableJobEnvelope job, CancellationToken cancellationToken)
        {
            recorder.PlatformCalls++;
            recorder.TrainerId = tenantContext.TrainerId;
            recorder.Origin = tenantContext.Origin;
            recorder.IsAdministrative = tenantContext.IsAdministrative;
            return Task.FromResult(DispatchItemOutcome.Succeeded());
        }
    }

    private sealed class TenantHandler(ContextRecorder recorder) : IDurableJobHandler
    {
        public string JobType => "tenant.test";
        public int JobVersion => 1;

        public Task<DispatchItemOutcome> HandleAsync(DurableJobEnvelope job, CancellationToken cancellationToken)
        {
            recorder.TenantCalls++;
            return Task.FromResult(DispatchItemOutcome.Succeeded());
        }
    }

    private sealed class ScriptedJobStore(DurableJob job) : IDurableJobStore
    {
        private bool _claimed;

        public int Completed { get; private set; }
        public string? FailureCode { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }

        // maxAttempts acrescentado pela correção PTM-SEC-06 (2026-09-14).
        public Task<IReadOnlyList<DurableJob>> ClaimDueJobsAsync(
            TimeSpan leaseDuration, int batchSize, int maxAttempts, CancellationToken cancellationToken)
        {
            IReadOnlyList<DurableJob> result = _claimed ? [] : [job];
            _claimed = true;
            return Task.FromResult(result);
        }

        public Task<bool> TryRenewLeaseAsync(
            Guid jobId, Guid leaseOwnerId, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> TryCompleteAsync(Guid jobId, Guid leaseOwnerId, CancellationToken cancellationToken)
        {
            Completed++;
            return Task.FromResult(true);
        }

        public Task<bool> TryRecordFailureAsync(
            Guid jobId, Guid leaseOwnerId, string sanitizedError, DateTime? nextAttemptAt,
            CancellationToken cancellationToken)
        {
            FailureCode = sanitizedError;
            NextAttemptAt = nextAttemptAt;
            return Task.FromResult(true);
        }
    }
}
