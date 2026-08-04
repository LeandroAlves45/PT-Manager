using Domain.Entities.Clients;
using Domain.Entities.Supplements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Supplements;

/// <summary>
/// Configura a atribuição direta e reforça a associação cliente-tenant na DB.
/// </summary>
internal sealed class ClientSupplementAssignmentConfiguration : IEntityTypeConfiguration<ClientSupplementAssignment>
{
    public void Configure(EntityTypeBuilder<ClientSupplementAssignment> builder)
    {
        builder.ToTable("client_supplement_assignments", table =>
        {
            table.HasCheckConstraint(
                "ck_client_supplement_assignments_serving_size",
                "btrim(serving_size) <> ''");
            table.HasCheckConstraint(
                "ck_client_supplement_assignments_timing",
                "btrim(timing) <> ''");
        });

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(assignment => assignment.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();
        builder.Property(assignment => assignment.ClientId)
            .HasColumnName("client_id")
            .IsRequired();
        builder.Property(assignment => assignment.SupplementId)
            .HasColumnName("supplement_id")
            .IsRequired();
        builder.Property(assignment => assignment.ServingSize)
            .HasColumnName("serving_size")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(assignment => assignment.Timing)
            .HasColumnName("timing")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(assignment => assignment.TrainerNotes)
            .HasColumnName("trainer_notes");
        builder.Property(assignment => assignment.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(assignment => assignment.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(assignment => assignment.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
        builder.Property(assignment => assignment.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(assignment =>
            new { assignment.ClientId, assignment.SupplementId })
            .HasDatabaseName("uq_client_supplement_active")
            .HasFilter("is_active = true AND is_deleted = false")
            .IsUnique();
        builder.HasIndex(assignment =>
            new { assignment.OwnerTrainerId, assignment.ClientId })
            .HasDatabaseName("idx_client_supplement_assignments_tenant_client")
            .HasFilter("is_deleted = false");
        builder.HasIndex(assignment => assignment.SupplementId)
            .HasDatabaseName("idx_client_supplement_assignments_supplement");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(assignment =>
                new { assignment.OwnerTrainerId, assignment.ClientId })
            .HasPrincipalKey(client => new { client.OwnerTrainerId, client.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_client_supplement_assignments_client_tenant");

        builder.HasOne<Supplement>()
            .WithMany()
            .HasForeignKey(assignment => assignment.SupplementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_client_supplement_assignments_supplement");
    }
}
