using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Billing;

/// <summary>
/// Representa a configuração da entidade TrainerSubscription para o Entity Framework Core.
/// </summary>
internal sealed class TrainerSubscriptionConfiguration : IEntityTypeConfiguration<TrainerSubscription>
{
    public void Configure(EntityTypeBuilder<TrainerSubscription> builder)
    {
        builder.ToTable("trainer_subscriptions");
        builder.HasKey(ts => ts.Id);
        builder.Property(ts => ts.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(ts => ts.TrainerId)
            .HasColumnName("trainer_id")
            .IsRequired();

        builder.Property(ts => ts.Status)
            .HasColumnName("subscription_status")
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion(vo => vo.Value, value => SubscriptionStatus.FromString(value))
            .HasDefaultValue(SubscriptionStatus.Active);

        builder.Property(ts => ts.Tier)
            .HasColumnName("subscription_tier")
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion(vo => vo.Value, value => SubscriptionTier.FromString(value))
            .HasDefaultValue(SubscriptionTier.Free);

        builder.Property(ts => ts.ClientLimit)
            .HasColumnName("client_limit")
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(ts => ts.CurrentClientCount)
            .HasColumnName("current_client_count")
            .HasDefaultValue(0);

        builder.Property(ts => ts.IsExemptFromBilling)
            .HasColumnName("is_exempt_from_billing")
            .HasDefaultValue(false);

        builder.Property(ts => ts.TrialEndsAt)
            .HasColumnName("trial_ends_at");

        builder.Property(ts => ts.StripeSubscriptionId)
            .HasColumnName("stripe_subscription_id")
            .HasMaxLength(255);

        builder.Property(ts => ts.StripeCustomerId)
            .HasColumnName("stripe_customer_id")
            .HasMaxLength(255);

        builder.Property(ts => ts.LastProviderStateObservedAt)
            .HasColumnName("last_provider_state_observed_at");

        builder.Property(ts => ts.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(ts => ts.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("status_check",
                "subscription_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'CANCELLED')");
            t.HasCheckConstraint("tier_check", "subscription_tier IN ('FREE', 'STARTER', 'PRO')");
        });

        builder.HasIndex(ts => ts.TrainerId)
            .HasDatabaseName("uq_trainer_subscriptions_trainer")
            .IsUnique();

        builder.HasIndex(ts => ts.StripeCustomerId)
            .HasDatabaseName("uq_trainer_subscriptions_stripe_customer")
            .HasFilter("stripe_customer_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(ts => ts.StripeSubscriptionId)
            .HasDatabaseName("uq_trainer_subscriptions_stripe_subscription")
            .HasFilter("stripe_subscription_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(ts => ts.Status).HasDatabaseName("idx_subscriptions_status");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ts => ts.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
