using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Sessions;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Sessions;

/// <summary>Configura sessões e a referência segura ao pack do cliente.</summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions", table =>
        {
            table.HasCheckConstraint("ck_sessions_duration", "duration_minutes > 0");
            table.HasCheckConstraint("ck_sessions_status",
                "status IN ('scheduled', 'completed', 'cancelled_by_client', 'cancelled_by_trainer', 'no_show')");
        });

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(session => session.OwnerTrainerId).HasColumnName("owner_trainer_id").IsRequired();
        builder.Property(session => session.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(session => session.ClientSessionPackId).HasColumnName("client_session_pack_id");
        builder.Property(session => session.StartsAt).HasColumnName("starts_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(session => session.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        builder.Property(session => session.Location).HasColumnName("location").HasMaxLength(255);
        builder.Property(session => session.SessionType).HasColumnName("session_type").HasMaxLength(50);
        builder.Property(session => session.Notes).HasColumnName("notes");
        builder.Property(session => session.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion(status => status.Value, value => SessionStatus.FromString(value))
            .IsRequired();
        builder.Property(session => session.StatusChangedAt).HasColumnName("status_changed_at").IsRequired();
        builder.Property(session => session.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(session => session.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(session => session.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(session => new { session.OwnerTrainerId, session.StartsAt })
            .HasDatabaseName("idx_sessions_tenant_scheduled_at")
            .HasFilter("status = 'scheduled' AND is_deleted = false");
        builder.HasIndex(session => new { session.OwnerTrainerId, session.ClientId, session.StartsAt })
            .HasDatabaseName("idx_sessions_tenant_client_starts_at");
        builder.HasIndex(session => session.ClientSessionPackId)
            .HasDatabaseName("idx_sessions_client_session_pack")
            .HasFilter("client_session_pack_id IS NOT NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_sessions_owner_trainer");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(session => new { session.OwnerTrainerId, session.ClientId })
            .HasPrincipalKey(client => new { client.OwnerTrainerId, client.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_sessions_client_tenant");

        // A FK inclui o cliente para impedir que uma sessão consuma o pack de outra pessoa.
        builder.HasOne<ClientSessionPack>()
            .WithMany()
            .HasForeignKey(session => new
            {
                session.OwnerTrainerId,
                session.ClientId,
                session.ClientSessionPackId
            })
            .HasPrincipalKey(pack => new
            {
                pack.OwnerTrainerId,
                pack.ClientId,
                ClientSessionPackId = pack.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sessions_client_pack_tenant");
    }
}
