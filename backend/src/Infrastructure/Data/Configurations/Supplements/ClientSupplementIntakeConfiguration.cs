using Domain.Entities.Clients;
using Domain.Entities.Supplements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Supplements;

/// <summary>
/// Configura a toma diária de suplemento: uma por atribuição e data local, com associação
/// client-tenant reforçada na DB.
/// </summary>
internal sealed class ClientSupplementIntakeConfiguration : IEntityTypeConfiguration<ClientSupplementIntake>
{
    public void Configure(EntityTypeBuilder<ClientSupplementIntake> builder)
    {
        builder.ToTable("client_supplement_intakes");
        builder.HasKey(intake => intake.Id);
        builder.Property(intake => intake.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(intake => intake.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(intake => intake.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(intake => intake.ClientSupplementAssignmentId)
            .HasColumnName("client_supplement_assignment_id")
            .IsRequired();

        builder.Property(intake => intake.LocalDate)
            .HasColumnName("local_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(intake => intake.TakenAt)
            .HasColumnName("taken_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(intake => new { intake.ClientSupplementAssignmentId, intake.LocalDate })
            .HasDatabaseName("uq_client_supplement_intakes_assignment_date")
            .IsUnique();

        builder.HasIndex(intake => new { intake.OwnerTrainerId, intake.ClientId, intake.LocalDate })
            .HasDatabaseName("idx_client_supplement_intakes_tenant_client_date");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(intake => new { intake.OwnerTrainerId, intake.ClientId })
            .HasPrincipalKey(client => new { client.OwnerTrainerId, client.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_client_supplement_intakes_client_tenant");

        builder.HasOne<ClientSupplementAssignment>()
            .WithMany()
            .HasForeignKey(intake => intake.ClientSupplementAssignmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_client_supplement_intakes_assignment");
    }
}
