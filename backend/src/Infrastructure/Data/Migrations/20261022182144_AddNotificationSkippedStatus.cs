using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSkippedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "status_check",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "status_check",
                table: "notifications",
                sql: "status IN ('pending', 'sent', 'failed', 'bounced', 'skipped')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "status_check",
                table: "notifications");

            // O estado antigo não conhece 'skipped': esses lembretes nunca saíram, ficam 'failed'.
            migrationBuilder.Sql("UPDATE notifications SET status = 'failed' WHERE status = 'skipped';");

            migrationBuilder.AddCheckConstraint(
                name: "status_check",
                table: "notifications",
                sql: "status IN ('pending', 'sent', 'failed', 'bounced')");
        }
    }
}
