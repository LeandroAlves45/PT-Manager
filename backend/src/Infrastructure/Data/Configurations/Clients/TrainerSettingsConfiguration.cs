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
            .ValueGeneratedOnAdd();

        builder.Property(settings => settings.TrainerId)
            .HasColumnName("trainer_id")
            .IsRequired();

        builder.Property(settings => settings.AppName)
            .HasColumnName("app_name")
            .HasMaxLength(255)
            .HasDefaultValue("PT Manager");

        builder.Property(settings => settings.LogoUrl)
            .HasColumnName("logo_url")
            .HasMaxLength(500);

        builder.Property(settings => settings.LogoPublicId)
            .HasColumnName("logo_public_id")
            .HasMaxLength(500);

        builder.Property(settings => settings.PrimaryColor)
            .HasColumnName("primary_color")
            .HasMaxLength(7)
            .HasDefaultValue("#000000");

        builder.Property(settings => settings.BodyColor)
            .HasColumnName("body_color")
            .HasMaxLength(7)
            .HasDefaultValue("#FFFFFF");

        builder.Property(settings => settings.BackgroundImageUrl)
            .HasColumnName("background_image_url")
            .HasMaxLength(500);

        builder.Property(settings => settings.Phone)
            .HasColumnName("phone")
            .HasMaxLength(20);

        builder.Property(settings => settings.Address)
            .HasColumnName("address")
            .HasMaxLength(500);

        builder.Property(settings => settings.City)
            .HasColumnName("city")
            .HasMaxLength(255);

        builder.HasIndex(settings => settings.TrainerId)
            .HasDatabaseName("idx_settings_trainer")
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(settings => settings.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
