using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint6AWriteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "default_serving_grams",
                table: "foods",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "planned_rpe",
                table: "exercise_sets",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rpe",
                table: "client_exercise_set_logs",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "checkins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_supplement_intakes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_supplement_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    taken_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_supplement_intakes", x => x.id);
                    table.ForeignKey(
                        name: "fk_client_supplement_intakes_assignment",
                        column: x => x.client_supplement_assignment_id,
                        principalTable: "client_supplement_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_client_supplement_intakes_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_completions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    training_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    training_plan_day_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_completions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workout_completions_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workout_completions_training_plan",
                        column: x => x.training_plan_id,
                        principalTable: "training_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workout_completions_training_plan_day",
                        column: x => x.training_plan_day_id,
                        principalTable: "training_plan_days",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_foods_default_serving_grams",
                table: "foods",
                sql: "default_serving_grams IS NULL OR (default_serving_grams > 0 AND default_serving_grams <= 1000)");

            migrationBuilder.AddCheckConstraint(
                name: "planned_rpe_check",
                table: "exercise_sets",
                sql: "planned_rpe IS NULL OR (planned_rpe >= 1 AND planned_rpe <= 10 AND planned_rpe * 2 = trunc(planned_rpe * 2))");

            migrationBuilder.AddCheckConstraint(
                name: "rpe_check",
                table: "client_exercise_set_logs",
                sql: "rpe IS NULL OR (rpe >= 1 AND rpe <= 10 AND rpe * 2 = trunc(rpe * 2))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_review_requires_response",
                table: "checkins",
                sql: "reviewed_at IS NULL OR (responded_at IS NOT NULL AND cancelled_at IS NULL)");

            migrationBuilder.CreateIndex(
                name: "idx_client_supplement_intakes_tenant_client_date",
                table: "client_supplement_intakes",
                columns: new[] { "owner_trainer_id", "client_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "uq_client_supplement_intakes_assignment_date",
                table: "client_supplement_intakes",
                columns: new[] { "client_supplement_assignment_id", "local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_workout_completions_day",
                table: "workout_completions",
                column: "training_plan_day_id");

            migrationBuilder.CreateIndex(
                name: "idx_workout_completions_plan",
                table: "workout_completions",
                column: "training_plan_id");

            migrationBuilder.CreateIndex(
                name: "idx_workout_completions_tenant_client_date",
                table: "workout_completions",
                columns: new[] { "owner_trainer_id", "client_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "uq_workout_completions_client_day_date",
                table: "workout_completions",
                columns: new[] { "client_id", "training_plan_day_id", "local_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preflight manual: o Down apaga histórico criado pelo cliente (séries com RPE,
            // conclusões, tomas) e dados do trainer (RPE planeado, porção, revisão). O rollback
            // exige exportar ou limpar esses dados de forma consciente antes de o executar.
            migrationBuilder.Sql("""
                DO $preflight$
                BEGIN
                    IF EXISTS (SELECT 1 FROM workout_completions)
                        OR EXISTS (SELECT 1 FROM client_supplement_intakes) THEN
                        RAISE EXCEPTION 'Rollback blocked: workout_completions or client_supplement_intakes contain client history.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM exercise_sets WHERE planned_rpe IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM client_exercise_set_logs WHERE rpe IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM foods WHERE default_serving_grams IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM checkins WHERE reviewed_at IS NOT NULL) THEN
                        RAISE EXCEPTION 'Rollback blocked: Sprint 6A columns contain data (planned_rpe, rpe, default_serving_grams or reviewed_at).';
                    END IF;
                END
                $preflight$;
                """);

            migrationBuilder.DropTable(
                name: "client_supplement_intakes");

            migrationBuilder.DropTable(
                name: "workout_completions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_foods_default_serving_grams",
                table: "foods");

            migrationBuilder.DropCheckConstraint(
                name: "planned_rpe_check",
                table: "exercise_sets");

            migrationBuilder.DropCheckConstraint(
                name: "rpe_check",
                table: "client_exercise_set_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_review_requires_response",
                table: "checkins");

            migrationBuilder.DropColumn(
                name: "default_serving_grams",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "planned_rpe",
                table: "exercise_sets");

            migrationBuilder.DropColumn(
                name: "rpe",
                table: "client_exercise_set_logs");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "checkins");
        }
    }
}
