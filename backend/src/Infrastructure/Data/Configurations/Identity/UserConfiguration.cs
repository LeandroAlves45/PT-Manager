using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>Configura a tabela de utilizadores usada pelos custom Identity stores.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_role",
                "role IN ('trainer', 'client', 'superuser')");
            table.HasCheckConstraint("ck_users_access_failed_count",
                "access_failed_count >= 0");
        });

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255);

        builder.Property(user => user.SecurityStamp)
            .HasColumnName("security_stamp")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(user => user.ConcurrencyStamp)
            .HasColumnName("concurrency_stamp")
            .HasMaxLength(255)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(user => user.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(255);

        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.EmailConfirmed)
            .HasColumnName("email_confirmed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(user => user.LockoutEnd)
            .HasColumnName("lockout_end");

        builder.Property(user => user.AccessFailedCount)
            .HasColumnName("access_failed_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(user => user.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("uq_users_normalized_email")
            .IsUnique();
        builder.HasIndex(user => user.Role)
            .HasDatabaseName("idx_users_role");
    }
}
