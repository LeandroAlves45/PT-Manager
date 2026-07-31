using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Notifications;

/// <summary>
/// Representa a configuração da entidade Notification.
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(n => n.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(n => n.ClientId)
            .HasColumnName("client_id");

        builder.Property(n => n.RecipientEmail)
            .HasColumnName("recipient_email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(n => n.NotificationType)
            .HasColumnName("notification_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.TemplateKey)
            .HasColumnName("template_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(n => n.TemplateData)
            .HasColumnName("template_data")
            .HasColumnType("jsonb");

        builder.Property(n => n.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasDefaultValue("pending");

        builder.Property(n => n.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(n => n.LastRetryAt)
            .HasColumnName("last_retry_at");

        builder.Property(n => n.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(n => n.SentAt)
            .HasColumnName("sent_at");

        builder.Property(n => n.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(n => n.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(tb => tb.HasCheckConstraint("status_check", "status IN ('pending', 'sent', 'failed', 'bounced')"));

        builder.HasIndex(n => n.OwnerTrainerId).HasDatabaseName("idx_notifications_trainer");
        builder.HasIndex(n => n.Status).HasDatabaseName("idx_notifications_status");
        builder.HasIndex(n => n.CreatedAt).HasDatabaseName("idx_notifications_created");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(n => new { n.OwnerTrainerId, n.ClientId })
            .HasPrincipalKey(c => new { c.OwnerTrainerId, c.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_client_tenant");
    }
}
