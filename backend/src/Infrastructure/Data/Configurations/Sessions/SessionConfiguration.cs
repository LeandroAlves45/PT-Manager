using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Sessions;

/// <summary>
/// Representa a configuração da entidade Session para o Entity Framework Core.
/// </summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(s => s.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(s => s.SessionDate)
            .HasColumnName("session_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(s => s.SessionTime)
            .HasColumnName("session_time")
            .HasColumnType("time");

        builder.Property(s => s.DurationMinutes)
            .HasColumnName("duration_minutes");

        builder.Property(s => s.SessionType)
            .HasColumnName("session_type")
            .HasMaxLength(50);

        builder.Property(s => s.Notes)
            .HasColumnName("notes");

        builder.Property(s => s.IsCompleted)
            .HasColumnName("is_completed")
            .HasDefaultValue(false);

        builder.Property(s => s.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint(
            "session_duration_positive",
            "duration_minutes IS NULL OR duration_minutes > 0")
        );

        builder.HasIndex(s => s.OwnerTrainerId).HasDatabaseName("idx_sessions_trainer");
        builder.HasIndex(s => s.ClientId).HasDatabaseName("idx_sessions_client");
        builder.HasIndex(s => s.SessionDate).HasDatabaseName("idx_sessions_date");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(s => new { s.OwnerTrainerId, s.ClientId })
            .HasPrincipalKey(c => new { c.OwnerTrainerId, c.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_sessions_client_tenant");
    }
}
