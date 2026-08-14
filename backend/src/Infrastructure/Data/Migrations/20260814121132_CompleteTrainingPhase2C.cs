using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteTrainingPhase2C : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_client_exercise_set_logs_training_plan_day_exercises_traini~",
                table: "client_exercise_set_logs");

            migrationBuilder.DropIndex(
                name: "IX_training_plans_owner_trainer_id_client_id",
                table: "training_plans");

            migrationBuilder.DropIndex(
                name: "idx_logs_client",
                table: "client_exercise_set_logs");

            migrationBuilder.DropIndex(
                name: "unique_set_log",
                table: "client_exercise_set_logs");

            migrationBuilder.RenameColumn(
                name: "logged_at",
                table: "client_exercise_set_logs",
                newName: "performed_at");

            migrationBuilder.CreateIndex(
                name: "uq_training_plan_active_per_client",
                table: "training_plans",
                columns: new[] { "owner_trainer_id", "client_id" },
                unique: true,
                filter: "is_active = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_training_plan_day_weekday",
                table: "training_plan_days",
                columns: new[] { "training_plan_id", "week_number", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_exercise_set_number",
                table: "exercise_sets",
                columns: new[] { "training_plan_day_exercise_id", "set_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_logs_client_performed_at",
                table: "client_exercise_set_logs",
                columns: new[] { "client_id", "performed_at", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.AddForeignKey(
                name: "FK_client_exercise_set_logs_training_plan_day_exercises_traini~",
                table: "client_exercise_set_logs",
                column: "training_plan_day_exercise_id",
                principalTable: "training_plan_day_exercises",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_client_exercise_set_logs_training_plan_day_exercises_traini~",
                table: "client_exercise_set_logs");

            migrationBuilder.DropIndex(
                name: "uq_training_plan_active_per_client",
                table: "training_plans");

            migrationBuilder.DropIndex(
                name: "uq_training_plan_day_weekday",
                table: "training_plan_days");

            migrationBuilder.DropIndex(
                name: "uq_exercise_set_number",
                table: "exercise_sets");

            migrationBuilder.DropIndex(
                name: "idx_logs_client_performed_at",
                table: "client_exercise_set_logs");

            migrationBuilder.RenameColumn(
                name: "performed_at",
                table: "client_exercise_set_logs",
                newName: "logged_at");

            migrationBuilder.CreateIndex(
                name: "IX_training_plans_owner_trainer_id_client_id",
                table: "training_plans",
                columns: new[] { "owner_trainer_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "idx_logs_client",
                table: "client_exercise_set_logs",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "unique_set_log",
                table: "client_exercise_set_logs",
                columns: new[] { "client_id", "training_plan_day_exercise_id", "set_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_client_exercise_set_logs_training_plan_day_exercises_traini~",
                table: "client_exercise_set_logs",
                column: "training_plan_day_exercise_id",
                principalTable: "training_plan_day_exercises",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
