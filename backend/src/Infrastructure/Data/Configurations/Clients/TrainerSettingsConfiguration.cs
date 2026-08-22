using Domain.Entities.Identity;
using Domain.Entities.TrainerSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Clients;

/// <summary>
/// Representa a configuração da entidade TrainerSettings para o Entity Framework Core.
/// </summary>
internal sealed class TrainerSettingsConfiguration : IEntityTypeConfiguration<TrainerSettings>
{
    public void Configure(EntityTypeBuilder<TrainerSettings> builder)
    {
        builder.ToTable("trainer_settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(settings => settings.TrainerId)
            .HasColumnName("trainer_id")
            .IsRequired();

        builder.Property(settings => settings.AppName)
            .HasColumnName("app_name")
            .HasMaxLength(50)
            .HasDefaultValue("PT Manager");

        builder.Property(settings => settings.LogoUrl)
            .HasColumnName("logo_url")
            .HasMaxLength(500);

        builder.Property(settings => settings.LogoPublicId)
            .HasColumnName("logo_public_id")
            .HasMaxLength(500);

        builder.Property(settings => settings.PrimaryColor)
            .HasColumnName("primary_color")
            .HasMaxLength(7);

        builder.Property(settings => settings.BodyColor)
            .HasColumnName("body_color")
            .HasMaxLength(7);

        builder.Property(settings => settings.Phone)
            .HasColumnName("phone")
            .HasMaxLength(20);

        builder.Property(settings => settings.Address)
            .HasColumnName("address")
            .HasMaxLength(500);

        builder.Property(settings => settings.City)
            .HasColumnName("city")
            .HasMaxLength(255);

        builder.Property(settings => settings.Timezone)
            .HasColumnName("time_zone_id")
            .HasMaxLength(100)
            .HasDefaultValue("Europe/Lisbon")
            .IsRequired();

        builder.HasIndex(settings => settings.TrainerId)
            .HasDatabaseName("idx_settings_trainer")
            .IsUnique();

        builder.Property(settings => settings.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(settings => settings.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(settings => settings.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
