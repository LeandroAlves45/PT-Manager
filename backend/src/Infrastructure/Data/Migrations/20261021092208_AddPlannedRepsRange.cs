using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedRepsRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "planned_reps_max",
                table: "exercise_sets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "reps_range_check",
                table: "exercise_sets",
                sql: "planned_reps_max IS NULL OR (planned_reps IS NOT NULL AND planned_reps_max > planned_reps)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "reps_range_check",
                table: "exercise_sets");

            migrationBuilder.DropColumn(
                name: "planned_reps_max",
                table: "exercise_sets");
        }
    }
}
