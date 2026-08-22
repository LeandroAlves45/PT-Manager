using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSprint3Phase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Interrompe antes da primeira alteração quando os dados exigem decisão humana.
            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM trainer_settings
                        WHERE char_length(app_name) > 50
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: trainer_settings.app_name exceeds 50 characters.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM supplements
                        WHERE created_by_user_id IS NULL
                          AND owner_trainer_id IS NULL
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: a global supplement has no explicit author.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM supplements
                        WHERE created_by_user_id = '00000000-0000-0000-0000-000000000000'::uuid
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: a supplement uses an empty author UUID.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM supplements
                        WHERE btrim(name) = ''
                           OR btrim(unit_of_measure) = ''
                           OR btrim(serving_size) = ''
                           OR btrim(timing) = ''
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: a required supplement field is blank.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM checkins
                        WHERE body_fat_percentage IN (0, 100)
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: a check-in has body fat equal to 0 or 100.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM sessions
                        WHERE status = 'scheduled'
                          AND is_deleted = false
                        GROUP BY owner_trainer_id, starts_at
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: duplicate scheduled sessions exist for the same trainer and start.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM pack_types
                        WHERE session_count <= 0
                           OR price_cents < 0
                           OR (duration_days IS NOT NULL AND duration_days <= 0)
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: a pack type violates the target constraints.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM client_session_packs
                        WHERE total_sessions <= 0
                           OR sessions_remaining < 0
                           OR sessions_remaining > total_sessions
                           OR price_cents < 0
                           OR (expiry_date IS NOT NULL AND expiry_date < purchase_date)
                    ) THEN
                        RAISE EXCEPTION 'CompleteSprint3Phase3 preflight failed: a client session pack violates the target constraints.';
                    END IF;
                END
                $migration$;
                """);

            // Preserva o significado funcional do arquivo antes de remover os marcadores legados.
            migrationBuilder.Sql(
                """
                UPDATE foods SET is_active = false WHERE is_deleted = true;
                UPDATE exercises SET is_active = false WHERE is_deleted = true;
                UPDATE supplements SET is_active = false WHERE is_deleted = true;
                UPDATE client_supplement_assignments
                SET is_active = false
                WHERE is_deleted = true;

                UPDATE supplements
                SET created_by_user_id = owner_trainer_id
                WHERE created_by_user_id IS NULL
                  AND owner_trainer_id IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_client_session_packs_users_owner_trainer_id",
                table: "client_session_packs");

            migrationBuilder.DropForeignKey(
                name: "FK_meal_plan_meal_items_foods_food_id",
                table: "meal_plan_meal_items");

            migrationBuilder.DropForeignKey(
                name: "FK_meal_plan_meal_supplements_supplements_supplement_id",
                table: "meal_plan_meal_supplements");

            migrationBuilder.DropForeignKey(
                name: "FK_pack_types_users_owner_trainer_id",
                table: "pack_types");

            migrationBuilder.DropForeignKey(
                name: "FK_supplements_users_created_by_user_id",
                table: "supplements");

            migrationBuilder.DropForeignKey(
                name: "FK_supplements_users_owner_trainer_id",
                table: "supplements");

            migrationBuilder.DropForeignKey(
                name: "FK_training_plan_day_exercises_exercises_exercise_id",
                table: "training_plan_day_exercises");

            migrationBuilder.DropIndex(
                name: "idx_supplements_name",
                table: "supplements");

            migrationBuilder.DropIndex(
                name: "idx_supplements_trainer",
                table: "supplements");

            migrationBuilder.DropIndex(
                name: "idx_sessions_tenant_scheduled_at",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "idx_packs_trainer",
                table: "pack_types");

            migrationBuilder.DropCheckConstraint(
                name: "pack_duration_positive",
                table: "pack_types");

            migrationBuilder.DropCheckConstraint(
                name: "pack_price_non_negative",
                table: "pack_types");

            migrationBuilder.DropCheckConstraint(
                name: "pack_session_count_positive",
                table: "pack_types");

            migrationBuilder.DropIndex(
                name: "IX_initial_assessments_owner_trainer_id_client_id",
                table: "initial_assessments");

            migrationBuilder.DropIndex(
                name: "uq_initial_assessments_client",
                table: "initial_assessments");

            migrationBuilder.DropIndex(
                name: "idx_client_supplement_assignments_tenant_client",
                table: "client_supplement_assignments");

            migrationBuilder.DropIndex(
                name: "uq_client_supplement_active",
                table: "client_supplement_assignments");

            migrationBuilder.DropIndex(
                name: "idx_client_packs_client",
                table: "client_session_packs");

            migrationBuilder.DropIndex(
                name: "idx_client_packs_trainer",
                table: "client_session_packs");

            migrationBuilder.DropIndex(
                name: "idx_client_packs_usable",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "pack_expiry_order",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "pack_sessions_consistent",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "pack_snapshot_price_non_negative",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "sessions_remaining_non_negative",
                table: "client_session_packs");

            migrationBuilder.DropIndex(
                name: "idx_checkins_client",
                table: "checkins");

            migrationBuilder.DropIndex(
                name: "idx_checkins_date",
                table: "checkins");

            migrationBuilder.DropIndex(
                name: "IX_checkins_owner_trainer_id_client_id",
                table: "checkins");

            migrationBuilder.DropIndex(
                name: "uq_checkins_client_date_active",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "checkin_body_fat_range",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "checkin_date_order",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "checkin_nutrition_adherence_range",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "checkin_training_adherence_range",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "checkin_weight_positive",
                table: "checkins");

            migrationBuilder.DropColumn(
                name: "background_image_url",
                table: "trainer_settings");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "supplements");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "client_supplement_assignments");

            migrationBuilder.RenameIndex(
                name: "IX_training_plan_day_exercises_exercise_id",
                table: "training_plan_day_exercises",
                newName: "idx_training_plan_day_exercises_exercise");

            migrationBuilder.RenameColumn(
                name: "duration_days",
                table: "pack_types",
                newName: "expected_duration_days");

            migrationBuilder.RenameIndex(
                name: "idx_pack_types_usable",
                table: "pack_types",
                newName: "idx_pack_types_tenant_name_active");

            migrationBuilder.RenameIndex(
                name: "IX_meal_plan_meal_items_food_id",
                table: "meal_plan_meal_items",
                newName: "idx_meal_plan_meal_items_food");

            migrationBuilder.RenameIndex(
                name: "idx_assessments_trainer",
                table: "initial_assessments",
                newName: "idx_initial_assessments_trainer");

            migrationBuilder.RenameColumn(
                name: "expiry_date",
                table: "client_session_packs",
                newName: "expected_end_date");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "primary_color",
                table: "trainer_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7,
                oldDefaultValue: "#000000");

            migrationBuilder.AlterColumn<string>(
                name: "body_color",
                table: "trainer_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7,
                oldDefaultValue: "#FFFFFF");

            migrationBuilder.AlterColumn<string>(
                name: "app_name",
                table: "trainer_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PT Manager",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldDefaultValue: "PT Manager");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_user_id",
                table: "supplements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "client_session_packs",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "checkins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "responded_at",
                table: "checkins",
                type: "timestamp with time zone",
                nullable: true);

            // Os timestamps legados são a aproximação determinística disponível.
            migrationBuilder.Sql(
                """
                UPDATE client_session_packs
                SET completed_at = updated_at
                WHERE sessions_remaining = 0
                  AND completed_at IS NULL;

                UPDATE checkins
                SET responded_at = updated_at
                WHERE weight_kg IS NOT NULL
                  AND responded_at IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "administrative_audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    before_state = table.Column<string>(type: "jsonb", nullable: true),
                    after_state = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_audit_entries", x => x.id);
                    table.CheckConstraint("ck_administrative_audit_entries_state", "before_state IS NOT NULL OR after_state IS NOT NULL");
                });

            migrationBuilder.CreateIndex(
                name: "idx_supplements_scope_active_name_id",
                table: "supplements",
                columns: new[] { "owner_trainer_id", "is_active", "name", "id" });

            migrationBuilder.CreateIndex(
                name: "idx_supplements_search_trgm",
                table: "supplements",
                columns: new[] { "description", "name" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops", "gin_trgm_ops" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplements_name",
                table: "supplements",
                sql: "btrim(name) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplements_serving_size",
                table: "supplements",
                sql: "btrim(serving_size) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplements_timing",
                table: "supplements",
                sql: "btrim(timing) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplements_unit",
                table: "supplements",
                sql: "btrim(unit_of_measure) <> ''");

            migrationBuilder.CreateIndex(
                name: "uq_sessions_tenant_scheduled_start",
                table: "sessions",
                columns: new[] { "owner_trainer_id", "starts_at" },
                unique: true,
                filter: "status = 'scheduled' AND is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pack_types_expected_duration_positive",
                table: "pack_types",
                sql: "expected_duration_days IS NULL OR expected_duration_days > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pack_types_price_non_negative",
                table: "pack_types",
                sql: "price_cents >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pack_types_session_count_positive",
                table: "pack_types",
                sql: "session_count > 0");

            migrationBuilder.CreateIndex(
                name: "uq_initial_assessments_tenant_client_active",
                table: "initial_assessments",
                columns: new[] { "owner_trainer_id", "client_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_foods_search_trgm",
                table: "foods",
                columns: new[] { "description", "name" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops", "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_exercises_search_trgm",
                table: "exercises",
                columns: new[] { "description", "name" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops", "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_client_supplement_assignments_list",
                table: "client_supplement_assignments",
                columns: new[] { "owner_trainer_id", "client_id", "is_active", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "uq_client_supplement_active",
                table: "client_supplement_assignments",
                columns: new[] { "owner_trainer_id", "client_id", "supplement_id" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_client_session_packs_usable_order",
                table: "client_session_packs",
                columns: new[] { "owner_trainer_id", "client_id", "expected_end_date", "created_at", "id" },
                filter: "sessions_remaining > 0 AND is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "ck_client_session_packs_balance",
                table: "client_session_packs",
                sql: "total_sessions > 0 AND sessions_remaining >= 0 AND sessions_remaining <= total_sessions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_client_session_packs_completion_consistency",
                table: "client_session_packs",
                sql: "(sessions_remaining = 0 AND completed_at IS NOT NULL) OR (sessions_remaining > 0 AND completed_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_client_session_packs_expected_end_order",
                table: "client_session_packs",
                sql: "expected_end_date IS NULL OR expected_end_date >= purchase_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_client_session_packs_price_non_negative",
                table: "client_session_packs",
                sql: "price_cents >= 0");

            migrationBuilder.CreateIndex(
                name: "idx_checkins_tenant_date_id",
                table: "checkins",
                columns: new[] { "owner_trainer_id", "check_in_date", "id" });

            migrationBuilder.CreateIndex(
                name: "uq_checkins_tenant_client_date_active",
                table: "checkins",
                columns: new[] { "owner_trainer_id", "client_id", "check_in_date" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_body_fat_range",
                table: "checkins",
                sql: "body_fat_percentage IS NULL OR (body_fat_percentage > 0 AND body_fat_percentage < 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_date_order",
                table: "checkins",
                sql: "target_date IS NULL OR target_date >= check_in_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_nutrition_adherence_range",
                table: "checkins",
                sql: "nutrition_adherence_score IS NULL OR nutrition_adherence_score BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_response_requires_weight",
                table: "checkins",
                sql: "responded_at IS NULL OR weight_kg IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_single_terminal_event",
                table: "checkins",
                sql: "NOT (responded_at IS NOT NULL AND cancelled_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_training_adherence_range",
                table: "checkins",
                sql: "training_adherence_score IS NULL OR training_adherence_score BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkins_weight_positive",
                table: "checkins",
                sql: "weight_kg IS NULL OR weight_kg > 0");

            migrationBuilder.CreateIndex(
                name: "idx_administrative_audit_actor_time",
                table: "administrative_audit_entries",
                columns: new[] { "actor_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "idx_administrative_audit_resource_time",
                table: "administrative_audit_entries",
                columns: new[] { "resource_type", "resource_id", "occurred_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_client_session_packs_owner_trainer",
                table: "client_session_packs",
                column: "owner_trainer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_meal_plan_meal_items_food",
                table: "meal_plan_meal_items",
                column: "food_id",
                principalTable: "foods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_meal_plan_meal_supplements_supplement",
                table: "meal_plan_meal_supplements",
                column: "supplement_id",
                principalTable: "supplements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pack_types_owner_trainer",
                table: "pack_types",
                column: "owner_trainer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_supplements_created_by_user",
                table: "supplements",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_supplements_owner_trainer",
                table: "supplements",
                column: "owner_trainer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_training_plan_day_exercises_exercise",
                table: "training_plan_day_exercises",
                column: "exercise_id",
                principalTable: "exercises",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_client_session_packs_owner_trainer",
                table: "client_session_packs");

            migrationBuilder.DropForeignKey(
                name: "fk_meal_plan_meal_items_food",
                table: "meal_plan_meal_items");

            migrationBuilder.DropForeignKey(
                name: "fk_meal_plan_meal_supplements_supplement",
                table: "meal_plan_meal_supplements");

            migrationBuilder.DropForeignKey(
                name: "fk_pack_types_owner_trainer",
                table: "pack_types");

            migrationBuilder.DropForeignKey(
                name: "fk_supplements_created_by_user",
                table: "supplements");

            migrationBuilder.DropForeignKey(
                name: "fk_supplements_owner_trainer",
                table: "supplements");

            migrationBuilder.DropForeignKey(
                name: "fk_training_plan_day_exercises_exercise",
                table: "training_plan_day_exercises");

            migrationBuilder.DropTable(
                name: "administrative_audit_entries");

            migrationBuilder.DropIndex(
                name: "idx_supplements_scope_active_name_id",
                table: "supplements");

            migrationBuilder.DropIndex(
                name: "idx_supplements_search_trgm",
                table: "supplements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplements_name",
                table: "supplements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplements_serving_size",
                table: "supplements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplements_timing",
                table: "supplements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplements_unit",
                table: "supplements");

            migrationBuilder.DropIndex(
                name: "uq_sessions_tenant_scheduled_start",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pack_types_expected_duration_positive",
                table: "pack_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pack_types_price_non_negative",
                table: "pack_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pack_types_session_count_positive",
                table: "pack_types");

            migrationBuilder.DropIndex(
                name: "uq_initial_assessments_tenant_client_active",
                table: "initial_assessments");

            migrationBuilder.DropIndex(
                name: "idx_foods_search_trgm",
                table: "foods");

            migrationBuilder.DropIndex(
                name: "idx_exercises_search_trgm",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "idx_client_supplement_assignments_list",
                table: "client_supplement_assignments");

            migrationBuilder.DropIndex(
                name: "uq_client_supplement_active",
                table: "client_supplement_assignments");

            migrationBuilder.DropIndex(
                name: "idx_client_session_packs_usable_order",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_client_session_packs_balance",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_client_session_packs_completion_consistency",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_client_session_packs_expected_end_order",
                table: "client_session_packs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_client_session_packs_price_non_negative",
                table: "client_session_packs");

            migrationBuilder.DropIndex(
                name: "idx_checkins_tenant_date_id",
                table: "checkins");

            migrationBuilder.DropIndex(
                name: "uq_checkins_tenant_client_date_active",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_body_fat_range",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_date_order",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_nutrition_adherence_range",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_response_requires_weight",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_single_terminal_event",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_training_adherence_range",
                table: "checkins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkins_weight_positive",
                table: "checkins");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "client_session_packs");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "checkins");

            migrationBuilder.DropColumn(
                name: "responded_at",
                table: "checkins");

            migrationBuilder.RenameIndex(
                name: "idx_training_plan_day_exercises_exercise",
                table: "training_plan_day_exercises",
                newName: "IX_training_plan_day_exercises_exercise_id");

            migrationBuilder.RenameColumn(
                name: "expected_duration_days",
                table: "pack_types",
                newName: "duration_days");

            migrationBuilder.RenameIndex(
                name: "idx_pack_types_tenant_name_active",
                table: "pack_types",
                newName: "idx_pack_types_usable");

            migrationBuilder.RenameIndex(
                name: "idx_meal_plan_meal_items_food",
                table: "meal_plan_meal_items",
                newName: "IX_meal_plan_meal_items_food_id");

            migrationBuilder.RenameIndex(
                name: "idx_initial_assessments_trainer",
                table: "initial_assessments",
                newName: "idx_assessments_trainer");

            migrationBuilder.RenameColumn(
                name: "expected_end_date",
                table: "client_session_packs",
                newName: "expiry_date");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "primary_color",
                table: "trainer_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#000000",
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "body_color",
                table: "trainer_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#FFFFFF",
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "app_name",
                table: "trainer_settings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "PT Manager",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "PT Manager");

            migrationBuilder.AddColumn<string>(
                name: "background_image_url",
                table: "trainer_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_user_id",
                table: "supplements",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "supplements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "foods",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "exercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "client_supplement_assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_supplements_name",
                table: "supplements",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_supplements_trainer",
                table: "supplements",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_tenant_scheduled_at",
                table: "sessions",
                columns: new[] { "owner_trainer_id", "starts_at" },
                filter: "status = 'scheduled' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_packs_trainer",
                table: "pack_types",
                column: "owner_trainer_id");

            migrationBuilder.AddCheckConstraint(
                name: "pack_duration_positive",
                table: "pack_types",
                sql: "duration_days IS NULL OR duration_days > 0");

            migrationBuilder.AddCheckConstraint(
                name: "pack_price_non_negative",
                table: "pack_types",
                sql: "price_cents >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "pack_session_count_positive",
                table: "pack_types",
                sql: "session_count > 0");

            migrationBuilder.CreateIndex(
                name: "IX_initial_assessments_owner_trainer_id_client_id",
                table: "initial_assessments",
                columns: new[] { "owner_trainer_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "uq_initial_assessments_client",
                table: "initial_assessments",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_client_supplement_assignments_tenant_client",
                table: "client_supplement_assignments",
                columns: new[] { "owner_trainer_id", "client_id" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_client_supplement_active",
                table: "client_supplement_assignments",
                columns: new[] { "client_id", "supplement_id" },
                unique: true,
                filter: "is_active = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_client_packs_client",
                table: "client_session_packs",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "idx_client_packs_trainer",
                table: "client_session_packs",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "idx_client_packs_usable",
                table: "client_session_packs",
                columns: new[] { "owner_trainer_id", "client_id", "expiry_date" },
                filter: "sessions_remaining > 0 AND is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "pack_expiry_order",
                table: "client_session_packs",
                sql: "expiry_date IS NULL OR expiry_date >= purchase_date");

            migrationBuilder.AddCheckConstraint(
                name: "pack_sessions_consistent",
                table: "client_session_packs",
                sql: "total_sessions > 0 AND sessions_remaining <= total_sessions");

            migrationBuilder.AddCheckConstraint(
                name: "pack_snapshot_price_non_negative",
                table: "client_session_packs",
                sql: "price_cents >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "sessions_remaining_non_negative",
                table: "client_session_packs",
                sql: "sessions_remaining >= 0");

            migrationBuilder.CreateIndex(
                name: "idx_checkins_client",
                table: "checkins",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "idx_checkins_date",
                table: "checkins",
                column: "check_in_date");

            migrationBuilder.CreateIndex(
                name: "IX_checkins_owner_trainer_id_client_id",
                table: "checkins",
                columns: new[] { "owner_trainer_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "uq_checkins_client_date_active",
                table: "checkins",
                columns: new[] { "client_id", "check_in_date" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddCheckConstraint(
                name: "checkin_body_fat_range",
                table: "checkins",
                sql: "body_fat_percentage IS NULL OR body_fat_percentage BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "checkin_date_order",
                table: "checkins",
                sql: "target_date IS NULL OR target_date >= check_in_date");

            migrationBuilder.AddCheckConstraint(
                name: "checkin_nutrition_adherence_range",
                table: "checkins",
                sql: "nutrition_adherence_score IS NULL OR nutrition_adherence_score BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "checkin_training_adherence_range",
                table: "checkins",
                sql: "training_adherence_score IS NULL OR training_adherence_score BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "checkin_weight_positive",
                table: "checkins",
                sql: "weight_kg IS NULL OR weight_kg > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_client_session_packs_users_owner_trainer_id",
                table: "client_session_packs",
                column: "owner_trainer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_meal_plan_meal_items_foods_food_id",
                table: "meal_plan_meal_items",
                column: "food_id",
                principalTable: "foods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_meal_plan_meal_supplements_supplements_supplement_id",
                table: "meal_plan_meal_supplements",
                column: "supplement_id",
                principalTable: "supplements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pack_types_users_owner_trainer_id",
                table: "pack_types",
                column: "owner_trainer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_supplements_users_created_by_user_id",
                table: "supplements",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_supplements_users_owner_trainer_id",
                table: "supplements",
                column: "owner_trainer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_training_plan_day_exercises_exercises_exercise_id",
                table: "training_plan_day_exercises",
                column: "exercise_id",
                principalTable: "exercises",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
