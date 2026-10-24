using Domain.Entities.Administration;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Administration;

/// <summary>Bloqueio da conta de um personal trainer: uma linha por personal trainer bloqueado.</summary>
internal sealed class TrainerAccessBlockConfiguration : IEntityTypeConfiguration<TrainerAccessBlock>
{
    public void Configure(EntityTypeBuilder<TrainerAccessBlock> builder)
    {
        builder.ToTable("trainer_access_blocks", table => table.HasCheckConstraint(
            "ck_trainer_access_blocks_reason",
            "reason IN ('malicious_content', 'dangerous_information', " +
            "'deliberately_false_information', 'prohibited_content')"));

        builder.HasKey(block => block.TrainerId);
        builder.Property(block => block.TrainerId)
            .HasColumnName("trainer_id")
            .ValueGeneratedNever();

        builder.Property(block => block.Reason)
            .HasColumnName("reason")
            .HasMaxLength(50)
            .HasConversion(reason => reason.Value, value => PlatformEnforcementReason.FromString(value))
            .IsRequired();

        builder.Property(block => block.Note)
            .HasColumnName("note")
            .HasMaxLength(PlatformEnforcementNotes.MaxLength);

        builder.Property(block => block.BlockedAt)
            .HasColumnName("blocked_at")
            .IsRequired();

        builder.Property(block => block.BlockedByUserId)
            .HasColumnName("blocked_by_user_id")
            .IsRequired();

        builder.Ignore(block => block.ClientAccessEndsAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.TrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_trainer_access_blocks_trainer");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_trainer_access_blocks_blocked_by");
    }
}
