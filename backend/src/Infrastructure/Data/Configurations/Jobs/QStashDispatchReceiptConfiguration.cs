using Infrastructure.Jobs.QStash;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Jobs;

/// <summary>Configuração EF Core dos recibos de replay QStash.</summary>
internal sealed class QStashDispatchReceiptConfiguration :
    IEntityTypeConfiguration<QStashDispatchReceipt>
{
    public void Configure(EntityTypeBuilder<QStashDispatchReceipt> builder)
    {
        builder.ToTable("qstash_dispatch_receipts");

        builder.HasKey(receipt => receipt.JtiHash)
            .HasName("pk_qstash_dispatch_receipts");

        builder.Property(receipt => receipt.JtiHash)
            .HasColumnName("jti_hash")
            .HasColumnType("character(64)")
            .IsRequired();

        builder.Property(receipt => receipt.TokenExpiresAt)
            .HasColumnName("token_expires_at")
            .IsRequired();

        builder.Property(receipt => receipt.ConsumedAt)
            .HasColumnName("consumed_at")
            .IsRequired();

        builder.HasIndex(receipt => receipt.TokenExpiresAt)
            .HasDatabaseName("ix_qstash_dispatch_receipts_token_expires_at");
    }
}
