using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Billing;

/// <summary>
/// Subscrição da plataforma por um personal trainer: tier (FREE/STARTER/PRO), estado,
/// limite de clientes e ligação ao Stripe.
/// </summary>
public sealed class TrainerSubscription
{
    public Guid Id { get; private set; }
    public Guid TrainerId { get; private set; }
    public SubscriptionStatus Status { get; private set; } = null!;
    public SubscriptionTier Tier { get; private set; } = null!;
    public int ClientLimit { get; private set; }
    public int CurrentClientCount { get; private set; }
    public bool IsExemptFromBilling { get; private set; }
    public DateTime? TrialEndsAt { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public DateTime? LastProviderStateObservedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TrainerSubscription() { }

    /// <summary>Cria a subscrição FREE inicial com trial ativado.</summary>
    public TrainerSubscription(
        Guid trainerId,
        DateTime trialEndsAt,
        DateTime now
    )
    {
        if (trialEndsAt <= now)
            throw new DomainException("Trial end date must be in the future.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        Status = SubscriptionStatus.Active;
        Tier = SubscriptionTier.Free;
        ClientLimit = 5;
        CurrentClientCount = 0;
        IsExemptFromBilling = false;
        TrialEndsAt = trialEndsAt;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// True se o personal trainer pode adicionar mais um cliente -> o gate usado pelo
    /// CreateClientHandler antes de criar um cliente.
    /// </summary>
    public bool CanAddClient() => IsExemptFromBilling || (Status == SubscriptionStatus.Active &&
        CurrentClientCount < ClientLimit);

    /// <summary>Incrementa a contagem de clientes ativos (na mesma transação da criação)</summary>
    public void RegisterClientAdded(DateTime now)
    {
        if (!CanAddClient())
            throw new DomainException("Client limit reached for the current subscription tier.");

        CurrentClientCount += 1;
        UpdatedAt = now;
    }

    /// <summary>Define se o personal trainer está isento de faturação.</summary>
    public void SetBillingExemption(bool isExempt, DateTime now)
    {
        IsExemptFromBilling = isExempt;
        UpdatedAt = now;
    }

    /// <summary>
    /// Decrementa a contagem de clientes ativos (cliente arquivado ou removido)
    /// Nunca abaixo de 0.
    /// </summary>
    public void RegisterClientRemoved(DateTime now)
    {
        if (CurrentClientCount > 0)
            CurrentClientCount -= 1;

        UpdatedAt = now;
    }

    /// <summary>
    /// Aplica uma mudança de tier confirmada pelo Stripe (upgrade/downgrade),
    /// atualizando o limite de clientes do novo tier.
    /// </summary>
    public void ChangeTier(SubscriptionTier tier, int clientLimit, DateTime now)
    {
        if (clientLimit < 0)
            throw new DomainException("Client limit cannot be negative.");

        Tier = tier;
        ClientLimit = clientLimit;
        UpdatedAt = now;
    }

    /// <summary>Associa o customer externo antes de existir uma subscription externa.</summary>
    public void LinkStripeCustomer(string customerId, DateTime now)
    {
        var normalized = NormalizeProviderId(customerId, "Stripe customer ID is invalid.");
        if (StripeCustomerId is not null && StripeCustomerId != normalized)
            throw new DomainException("A different Stripe customer is already linked.");
        if (StripeCustomerId == normalized)
            return;

        StripeCustomerId = normalized;
        UpdatedAt = now;
    }

    /// <summary>Associa uma subscription externa ao customer já validado.</summary>
    public void LinkStripeSubscription(string customerId, string subscriptionId, DateTime now)
    {
        var normalizedCustomerId = NormalizeProviderId(
            customerId,
            "Stripe customer ID is invalid.");
        var normalizedSubscriptionId = NormalizeProviderId(
            subscriptionId,
            "Stripe subscription ID is invalid.");

        if (StripeCustomerId is not null && StripeCustomerId != normalizedCustomerId)
            throw new DomainException("A different Stripe customer is already linked.");

        var mayReplace = Status == SubscriptionStatus.Inactive ||
            Status == SubscriptionStatus.Cancelled;
        if (StripeSubscriptionId is not null &&
            StripeSubscriptionId != normalizedSubscriptionId &&
            !mayReplace)
        {
            throw new DomainException("An active Stripe subscription cannot be replaced.");
        }

        if (StripeCustomerId == normalizedCustomerId &&
            StripeSubscriptionId == normalizedSubscriptionId)
        {
            return;
        }

        StripeCustomerId = normalizedCustomerId;
        StripeSubscriptionId = normalizedSubscriptionId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Aplica um snapshot autoritativo apenas quando foi observado depois do último
    /// estado aceite. A verificação acontece antes de qualquer mutação para impedir
    /// que webhooks concorrentes façam regredir a subscrição.
    /// </summary>
    public bool ApplyProviderSnapshot(
        string customerId,
        string subscriptionId,
        SubscriptionTier tier,
        int clientLimit,
        SubscriptionStatus status,
        DateTime? trialEndsAt,
        DateTime observedAt,
        DateTime now
    )
    {
        var normalizedCustomerId = NormalizeProviderId(
            customerId,
            "Stripe customer ID is invalid.");
        var normalizedSubscriptionId = NormalizeProviderId(
            subscriptionId,
            "Stripe subscription ID is invalid.");
        ArgumentNullException.ThrowIfNull(tier);
        ArgumentNullException.ThrowIfNull(status);

        if (clientLimit < 0)
            throw new DomainException("Client limit cannot be negative.");
        if (observedAt == default || observedAt.Kind != DateTimeKind.Utc)
            throw new DomainException("Provider state observation time must be UTC.");
        if (StripeCustomerId is not null && StripeCustomerId != normalizedCustomerId)
            throw new DomainException("A different Stripe customer is already linked.");

        var mayReplaceSubscription = Status == SubscriptionStatus.Inactive ||
            Status == SubscriptionStatus.Cancelled;
        if (StripeSubscriptionId is not null &&
            StripeSubscriptionId != normalizedSubscriptionId &&
            !mayReplaceSubscription)
        {
            throw new DomainException("An active Stripe subscription cannot be replaced.");
        }

        if (LastProviderStateObservedAt.HasValue &&
            observedAt <= LastProviderStateObservedAt.Value)
        {
            return false;
        }

        StripeCustomerId = normalizedCustomerId;
        StripeSubscriptionId = normalizedSubscriptionId;
        Tier = tier;
        ClientLimit = clientLimit;
        Status = status;
        TrialEndsAt = trialEndsAt;
        LastProviderStateObservedAt = observedAt;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Reconcilia tier, limite, estado e trial a partir do provider.</summary>
    public void ApplyBillingState(
        SubscriptionTier tier,
        int clientLimit,
        SubscriptionStatus status,
        DateTime? trialEndsAt,
        DateTime now
    )
    {
        ArgumentNullException.ThrowIfNull(tier);
        ArgumentNullException.ThrowIfNull(status);
        if (clientLimit < 0)
            throw new DomainException("Client limit cannot be negative.");

        Tier = tier;
        ClientLimit = clientLimit;
        Status = status;
        TrialEndsAt = trialEndsAt;
        UpdatedAt = now;
    }

    /// <summary>Transições de estado vindas de eventos do Stripe.</summary>
    public void Activate(DateTime now)
    {
        Status = SubscriptionStatus.Active;
        UpdatedAt = now;
    }
    public void Suspend(DateTime now)
    {
        Status = SubscriptionStatus.Suspended;
        UpdatedAt = now;
    }
    public void Cancel(DateTime now)
    {
        Status = SubscriptionStatus.Cancelled;
        UpdatedAt = now;
    }
    public void Deactivate(DateTime now)
    {
        Status = SubscriptionStatus.Inactive;
        UpdatedAt = now;
    }

    private static string NormalizeProviderId(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 255)
            throw new DomainException(message);

        return value.Trim();
    }
}
