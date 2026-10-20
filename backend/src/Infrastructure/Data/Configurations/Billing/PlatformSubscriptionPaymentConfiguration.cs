using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Billing;

/// <summary>Configura os pagamentos confirmados das subscrições da plataforma.</summary>
internal sealed class PlatformSubscriptionPaymentConfiguration
    : IEntityTypeConfiguration<PlatformSubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<PlatformSubscriptionPayment> builder)
    {
        builder.ToTable("platform_subscription_payments", table =>
        {
            table.HasCheckConstraint(
                "ck_platform_subscription_payments_amount",
                "amount_paid_cents > 0 AND net_cents <= amount_paid_cents");
            table.HasCheckConstraint(
                "ck_platform_subscription_payments_currency",
                "char_length(currency) = 3");
        });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(payment => payment.TrainerId).HasColumnName("trainer_id").IsRequired();
        builder.Property(payment => payment.StripeInvoiceId)
            .HasColumnName("stripe_invoice_id")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(payment => payment.AmountPaidCents)
            .HasColumnName("amount_paid_cents")
            .IsRequired();
        builder.Property(payment => payment.NetCents).HasColumnName("net_cents").IsRequired();
        builder.Property(payment => payment.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(payment => payment.PaidAt).HasColumnName("paid_at").IsRequired();
        builder.Property(payment => payment.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(payment => payment.StripeInvoiceId)
            .HasDatabaseName("uq_platform_subscription_payments_invoice")
            .IsUnique();
        builder.HasIndex(payment => payment.PaidAt)
            .HasDatabaseName("idx_platform_subscription_payments_paid_at");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(payment => payment.TrainerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_platform_subscription_payments_trainer");
    }
}
