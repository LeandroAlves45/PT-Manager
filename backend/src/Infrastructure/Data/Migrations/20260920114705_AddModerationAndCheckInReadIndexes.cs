using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationAndCheckInReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_foods_moderation_queue",
                table: "foods",
                columns: new[] { "updated_at", "id" },
                descending: new[] { true, false },
                filter: "owner_trainer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_moderation_queue",
                table: "exercises",
                columns: new[] { "updated_at", "id" },
                descending: new[] { true, false },
                filter: "owner_trainer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_checkins_pending_review",
                table: "checkins",
                columns: new[] { "owner_trainer_id", "responded_at", "id" },
                filter: "responded_at IS NOT NULL AND cancelled_at IS NULL AND reviewed_at IS NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_foods_moderation_queue",
                table: "foods");

            migrationBuilder.DropIndex(
                name: "idx_exercises_moderation_queue",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "idx_checkins_pending_review",
                table: "checkins");
        }
    }
}
