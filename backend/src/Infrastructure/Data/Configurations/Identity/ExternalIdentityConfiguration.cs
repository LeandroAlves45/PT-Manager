using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>Configura o mapeamento da entidade ExternalIdentity para a base de dados.</summary>
internal sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("external_identities", table =>
            table.HasCheckConstraint("ck_external_identities_provider", "provider IN ('google')"));

        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(identity => identity.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(identity => identity.Provider)
            .HasColumnName("provider")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(identity => identity.Subject)
            .HasColumnName("subject")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(identity => identity.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(identity => identity.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(identity => new { identity.Provider, identity.Subject })
            .HasDatabaseName("uq_external_identities_provider_subject")
            .IsUnique();

        builder.HasIndex(identity => new { identity.UserId, identity.Provider })
            .HasDatabaseName("uq_external_identities_user_provider")
            .IsUnique();
    }
}
