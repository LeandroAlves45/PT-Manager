using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateCatalogEnforcement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "platform_enforced_at",
                table: "foods",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_reason",
                table: "foods",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_status",
                table: "foods",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "allowed");

            migrationBuilder.AddColumn<DateTime>(
                name: "platform_enforced_at",
                table: "exercises",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_reason",
                table: "exercises",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_enforcement_status",
                table: "exercises",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "allowed");

            migrationBuilder.AddCheckConstraint(
                name: "ck_foods_platform_enforcement",
                table: "foods",
                sql: "(platform_enforcement_status = 'allowed' AND platform_enforcement_reason IS NULL AND platform_enforced_at IS NULL) OR (platform_enforcement_status = 'blocked' AND owner_trainer_id IS NOT NULL AND platform_enforcement_reason IS NOT NULL AND platform_enforcement_reason IN ('malicious_content', 'dangerous_information', 'deliberately_false_information', 'prohibited_content') AND platform_enforced_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exercises_platform_enforcement",
                table: "exercises",
                sql: "(platform_enforcement_status = 'allowed' AND platform_enforcement_reason IS NULL AND platform_enforced_at IS NULL) OR (platform_enforcement_status = 'blocked' AND owner_trainer_id IS NOT NULL AND platform_enforcement_reason IS NOT NULL AND platform_enforcement_reason IN ('malicious_content', 'dangerous_information', 'deliberately_false_information', 'prohibited_content') AND platform_enforced_at IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_foods_platform_enforcement",
                table: "foods");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exercises_platform_enforcement",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "platform_enforced_at",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_reason",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_status",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "platform_enforced_at",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_reason",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "platform_enforcement_status",
                table: "exercises");
        }
    }
}
