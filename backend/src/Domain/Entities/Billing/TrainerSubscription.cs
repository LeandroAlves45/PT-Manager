using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Billing;

/// <summary>
/// Subscrição da plataforma por um personal trainer: tier (FREE/STARTER/PRO), estado,
/// limite de clientes e ligação ao Stripe.
/// </summary>
public class TrainerSubscription
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

    /// <summary>Liga a subcrição ás referências do Stripe (checkout concluído).</summary>
    public void LinkStripe(string customerId, string subscriptionId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(subscriptionId))
            throw new DomainException("Stripe references cannot be empty.");

        var trimmedCustomerId = customerId.Trim();
        var trimmedSubscriptionId = subscriptionId.Trim();
        if (trimmedCustomerId.Length > 255)
            throw new DomainException("Stripe customer ID cannot exceed 255 characters.");
        if (trimmedSubscriptionId.Length > 255)
            throw new DomainException("Stripe subscription ID cannot exceed 255 characters.");

        StripeCustomerId = trimmedCustomerId;
        StripeSubscriptionId = trimmedSubscriptionId;
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
}
