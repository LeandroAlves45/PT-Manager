using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Billing;

/// <summary>
/// Representa a configuração da entidade ClientSessionPack para o Entity Framework Core.
/// </summary>
internal sealed class ClientSessionPackConfiguration : IEntityTypeConfiguration<ClientSessionPack>
{
    public void Configure(EntityTypeBuilder<ClientSessionPack> builder)
    {
        builder.ToTable("client_session_packs");
        builder.HasKey(csp => csp.Id);
        builder.Property(csp => csp.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(csp => csp.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(csp => csp.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(csp => csp.PackTypeId)
            .HasColumnName("pack_type_id")
            .IsRequired();

        builder.Property(csp => csp.PackName)
            .HasColumnName("pack_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(csp => csp.SessionsTotal)
            .HasColumnName("total_sessions")
            .IsRequired();

        builder.Property(csp => csp.SessionsRemaining)
            .HasColumnName("sessions_remaining")
            .IsRequired();

        builder.Property(csp => csp.PriceCents)
            .HasColumnName("price_cents")
            .IsRequired();

        builder.Property(csp => csp.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(csp => csp.PurchaseDate)
            .HasColumnName("purchase_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(csp => csp.ExpirationDate)
            .HasColumnName("expiry_date")
            .HasColumnType("date");

        builder.Property(csp => csp.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(csp => csp.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(csp => csp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("sessions_remaining_non_negative", "sessions_remaining >= 0");
            t.HasCheckConstraint(
                "pack_sessions_consistent",
                "total_sessions > 0 AND sessions_remaining <= total_sessions");
            t.HasCheckConstraint("pack_snapshot_price_non_negative", "price_cents >= 0");
            t.HasCheckConstraint("pack_expiry_order", "expiry_date IS NULL OR expiry_date >= purchase_date");
        });

        builder.HasIndex(csp => csp.OwnerTrainerId).HasDatabaseName("idx_client_packs_trainer");
        builder.HasIndex(csp => csp.ClientId).HasDatabaseName("idx_client_packs_client");
        builder.HasIndex(csp => new
        {
            csp.OwnerTrainerId,
            csp.ClientId,
            csp.ExpirationDate,
        })
            .HasDatabaseName("idx_client_packs_usable")
            .HasFilter("sessions_remaining > 0 AND is_deleted = false");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(csp => csp.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(csp => new { csp.OwnerTrainerId, csp.ClientId })
            .HasPrincipalKey(c => new { c.OwnerTrainerId, c.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_client_session_packs_client_tenant");

        builder.HasOne<PackType>()
            .WithMany()
            .HasForeignKey(csp => new { csp.OwnerTrainerId, csp.PackTypeId })
            .HasPrincipalKey(pt => new { pt.OwnerTrainerId, pt.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_client_session_packs_pack_type_tenant");
    }
}
