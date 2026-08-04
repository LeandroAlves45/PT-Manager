using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Clients;

/// <summary>Configura a ficha do cliente e a associação opcional à conta.</summary>
internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients", table =>
        {
            table.HasCheckConstraint("ck_clients_sex",
                "sex IN ('male', 'female')");
        });

        builder.HasKey(client => client.Id);
        builder.Property(client => client.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(client => client.OwnerTrainerId).HasColumnName("owner_trainer_id").IsRequired();
        builder.Property(client => client.UserId).HasColumnName("user_id");
        builder.Property(client => client.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(client => client.ContactEmail).HasColumnName("contact_email").HasMaxLength(255);
        builder.Property(client => client.NormalizedContactEmail).HasColumnName("normalized_contact_email").HasMaxLength(255);
        builder.Property(client => client.Phone).HasColumnName("phone").HasMaxLength(32).IsRequired();
        builder.Property(client => client.BirthDate)
            .HasColumnName("date_of_birth")
            .HasColumnType("date")
            .HasConversion(
                birthDate => birthDate.Value,
                value => BirthDate.FromPersisted(value))
            .IsRequired();
        builder.Property(client => client.Sex)
            .HasColumnName("sex")
            .HasMaxLength(6)
            .HasConversion(
                sex => sex.Value,
                value => BiologicalSex.FromString(value))
            .IsRequired();
        builder.Property(client => client.Objective).HasColumnName("objective").HasMaxLength(255);
        builder.Property(client => client.Notes).HasColumnName("notes");
        builder.Property(client => client.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(255);
        builder.Property(client => client.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(32);
        builder.Property(client => client.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
        builder.Property(client => client.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(client => client.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(client => client.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
        builder.Property(client => client.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(client => client.OwnerTrainerId)
            .HasDatabaseName("idx_clients_owner_trainer");
        builder.HasIndex(client => new { client.OwnerTrainerId, client.Id })
            .HasDatabaseName("uq_clients_tenant_id")
            .IsUnique();
        builder.HasIndex(client => client.UserId)
            .HasDatabaseName("uq_clients_user")
            .HasFilter("user_id IS NOT NULL")
            .IsUnique();
        builder.HasIndex(client => new { client.OwnerTrainerId, client.NormalizedContactEmail })
            .HasDatabaseName("uq_clients_tenant_contact_email_active")
            .HasFilter("normalized_contact_email IS NOT NULL AND is_deleted = false")
            .IsUnique();
        builder.HasIndex(client => new { client.OwnerTrainerId, client.Phone })
            .HasDatabaseName("uq_clients_tenant_phone_active")
            .HasFilter("is_deleted = false")
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(client => client.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_clients_owner_trainer");

        // A ficha profissional deve sobreviver à remoção da conta de acesso.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(client => client.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_clients_user");
    }
}
