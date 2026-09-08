using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

/// <summary>IDs estáveis dos eventos operacionais de execução durável.</summary>
public static class JobDispatchLogEvents
{
    public static readonly EventId ActivationStarted = new(3000, nameof(ActivationStarted));
    public static readonly EventId ActivationFailure = new(3001, nameof(ActivationFailure));
    public static readonly EventId ItemSucceeded = new(3002, nameof(ItemSucceeded));
    public static readonly EventId ItemRetryScheduled = new(3003, nameof(ItemRetryScheduled));
    public static readonly EventId ItemDeadLettered = new(3004, nameof(ItemDeadLettered));
    public static readonly EventId LeaseLost = new(3005, nameof(LeaseLost));
    public static readonly EventId TenantRejected = new(3006, nameof(TenantRejected));
    public static readonly EventId RequestRejected = new(3007, nameof(RequestRejected));
}
