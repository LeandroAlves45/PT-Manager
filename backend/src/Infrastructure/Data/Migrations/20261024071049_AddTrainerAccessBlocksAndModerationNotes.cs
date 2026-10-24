using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerAccessBlocksAndModerationNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "retired_at",
                table: "training_plan_day_exercises",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "platform_enforced_at",
                table: "supplements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_note",
                table: "supplements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_reason",
                table: "supplements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_status",
                table: "supplements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "allowed");

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_note",
                table: "foods",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_note",
                table: "exercises",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trainer_access_blocks",
                columns: table => new
                {
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    blocked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_access_blocks", x => x.trainer_id);
                    table.CheckConstraint("ck_trainer_access_blocks_reason", "reason IN ('malicious_content', 'dangerous_information', 'deliberately_false_information', 'prohibited_content')");
                    table.ForeignKey(
                        name: "fk_trainer_access_blocks_blocked_by",
                        column: x => x.blocked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trainer_access_blocks_trainer",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplements_platform_enforcement",
                table: "supplements",
                sql: "(platform_enforcement_status = 'allowed' AND platform_enforcement_reason IS NULL AND platform_enforced_at IS NULL AND platform_enforcement_note IS NULL) OR (platform_enforcement_status = 'blocked' AND owner_trainer_id IS NOT NULL AND platform_enforcement_reason IN ('malicious_content', 'dangerous_information', 'deliberately_false_information', 'prohibited_content') AND platform_enforced_at IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_access_blocks_blocked_by_user_id",
                table: "trainer_access_blocks",
                column: "blocked_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trainer_access_blocks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplements_platform_enforcement",
                table: "supplements");

            migrationBuilder.DropColumn(
                name: "retired_at",
                table: "training_plan_day_exercises");

            migrationBuilder.DropColumn(
                name: "platform_enforced_at",
                table: "supplements");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_note",
                table: "supplements");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_reason",
                table: "supplements");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_status",
                table: "supplements");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_note",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_note",
                table: "exercises");
        }
    }
}
