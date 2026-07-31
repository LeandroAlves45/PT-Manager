using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Clients;

/// <summary>
/// Representa a configuração da entidade Client para o Entity Framework Core.
/// </summary>
internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(client => client.Id);
        builder.Property(client => client.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(client => client.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(client => client.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(client => client.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(client => client.Objective)
            .HasColumnName("objective")
            .HasMaxLength(255);

        builder.Property(client => client.Bio)
            .HasColumnName("bio");

        builder.Property(client => client.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(500);

        builder.Property(client => client.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(client => client.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(client => client.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(client => client.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        // Duas Fk com user_id (conta do cliente) e owner_trainer_id (personal trainer do cliente)
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(client => client.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(client => client.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(client => new { client.OwnerTrainerId, client.UserId })
            .HasDatabaseName("unique_client_per_trainer")
            .IsUnique();
        builder.HasIndex(client => client.OwnerTrainerId).HasDatabaseName("idx_clients_trainer");

        builder.HasIndex(client => new { client.OwnerTrainerId, client.Id })
            .HasDatabaseName("uq_clients_tenant_id")
            .IsUnique();
    }
}
