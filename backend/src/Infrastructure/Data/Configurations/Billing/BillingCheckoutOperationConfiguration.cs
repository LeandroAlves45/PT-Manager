using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Billing;

/// <summary>Configura persistência, concorrência e unicidade das intenções do Checkout.</summary>
internal sealed class BillingCheckoutOperationConfiguration
    : IEntityTypeConfiguration<BillingCheckoutOperation>
{
    public void Configure(EntityTypeBuilder<BillingCheckoutOperation> builder)
    {
        builder.ToTable("billing_checkout_operations", table =>
        {
            table.HasCheckConstraint("ck_billing_checkout_operations_status",
                "status IN ('pending', 'created', 'completed', 'expired', 'failed')");
            table.HasCheckConstraint("ck_billing_checkout_operations_lease",
                "(status = 'pending' AND lease_owner_id IS NOT NULL AND lease_expires_at IS NOT NULL) OR " +
                "(status <> 'pending' AND lease_owner_id IS NULL AND lease_expires_at IS NULL)");
        });

        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(operation => operation.TrainerId)
            .HasColumnName("trainer_id")
            .IsRequired();

        builder.Property(operation => operation.ClientOperationId)
            .HasColumnName("client_operation_id")
            .IsRequired();

        builder.Property(operation => operation.Tier)
            .HasColumnName("tier")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(value => value.Value, value => SubscriptionTier.FromString(value));

        builder.Property(operation => operation.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(value => value.Value, value => BillingCheckoutOperationStatus.FromString(value));

        builder.Property(operation => operation.LeaseOwnerId)
            .HasColumnName("lease_owner_id");

        builder.Property(operation => operation.LeaseExpiresAt)
            .HasColumnName("lease_expires_at");

        builder.Property(operation => operation.EffectiveTrialEndsAt)
            .HasColumnName("effective_trial_ends_at");

        builder.Property(operation => operation.StripeCheckoutSessionId)
            .HasColumnName("stripe_checkout_session_id")
            .HasMaxLength(255);

        builder.Property(operation => operation.StripeSessionExpiresAt)
            .HasColumnName("stripe_session_expires_at");

        builder.Property(operation => operation.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(255);

        builder.Property(operation => operation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(operation => operation.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(operation => new { operation.TrainerId, operation.ClientOperationId })
            .HasDatabaseName("uq_billing_checkout_operations_trainer_client_operation")
            .IsUnique();

        builder.HasIndex(operation => operation.TrainerId)
            .HasDatabaseName("uq_billing_checkout_operations_active_trainer")
            .HasFilter("status IN ('pending', 'created')")
            .IsUnique();

        builder.HasIndex(operation => new { operation.Status, operation.LeaseExpiresAt })
            .HasDatabaseName("ix_billing_checkout_operations_status_lease_expires_at");

        builder.HasIndex(operation => operation.StripeCheckoutSessionId)
            .HasDatabaseName("ix_billing_checkout_operations_stripe_session")
            .HasFilter("stripe_checkout_session_id IS NOT NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(operation => operation.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
