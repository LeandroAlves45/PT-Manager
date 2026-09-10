using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Estado persistido de uma intenção de Checkout.</summary>
public sealed record BillingCheckoutOperationStatus
{
    public static readonly BillingCheckoutOperationStatus Pending = new("pending");
    public static readonly BillingCheckoutOperationStatus Created = new("created");
    public static readonly BillingCheckoutOperationStatus Completed = new("completed");
    public static readonly BillingCheckoutOperationStatus Expired = new("expired");
    public static readonly BillingCheckoutOperationStatus Failed = new("failed");

    private BillingCheckoutOperationStatus(string value) => Value = value;

    public string Value { get; }

    public static BillingCheckoutOperationStatus FromString(string value) => value switch
    {
        "pending" => Pending,
        "created" => Created,
        "completed" => Completed,
        "expired" => Expired,
        "failed" => Failed,
        _ => throw new DomainException($"Invalid billing checkout operation status: {value}")
    };
}
