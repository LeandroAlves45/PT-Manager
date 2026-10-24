using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialAssessmentClientSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "initial_assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "submitted_by_client",
                table: "initial_assessments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_initial_assessments_review_requires_client_submission",
                table: "initial_assessments",
                sql: "reviewed_at IS NULL OR submitted_by_client");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_initial_assessments_review_requires_client_submission",
                table: "initial_assessments");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "initial_assessments");

            migrationBuilder.DropColumn(
                name: "submitted_by_client",
                table: "initial_assessments");
        }
    }
}
