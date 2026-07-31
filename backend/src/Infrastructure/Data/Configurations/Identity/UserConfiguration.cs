using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>
/// Representa a configuração da entidade User para o Entity Framework Core.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // A nullability transitória é uma necessidade de construção do
        // Identity, não um estado persistível. InviteToken representa o
        // convite até existir hash.
        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.SecurityStamp)
            .HasColumnName("security_stamp")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.ConcurrencyStamp)
            .HasColumnName("concurrency_stamp")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(255);

        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.EmailConfirmed)
            .HasColumnName("email_confirmed")
            .HasDefaultValue(false);

        builder.Property(user => user.LockoutEnd)
            .HasColumnName("lockout_end");

        builder.Property(user => user.AccessFailedCount)
            .HasColumnName("access_failed_count")
            .HasDefaultValue(0);

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(user => user.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("normalized_email_unique")
            .IsUnique();

        builder.HasIndex(user => user.Role)
            .HasDatabaseName("idx_users_role");

        builder.ToTable(t => t.HasCheckConstraint(
            "role_check",
            "role IN ('trainer', 'client', 'superuser')"
        ));
    }
}
