using Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Billing;

/// <summary>
/// Representa a configuração da entidade ProcessedStripeEvent para o Entity Framework Core.
/// </summary>
internal sealed class ProcessedStripeEventConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("processed_stripe_events");
        builder.HasKey(pse => pse.Id);
        builder.Property(pse => pse.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(pse => pse.StripeEventId)
            .HasColumnName("stripe_event_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(pse => pse.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(pse => pse.ProcessedAt)
            .HasColumnName("processed_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(pse => pse.StripeEventId).IsUnique();
        builder.HasIndex(pse => pse.EventType).HasDatabaseName("idx_stripe_events_type");
    }
}
