using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_stripe_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_stripe_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    security_stamp = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    concurrency_stamp = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    lockout_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_access_failed_count", "access_failed_count >= 0");
                    table.CheckConstraint("ck_users_role", "role IN ('trainer', 'client', 'superuser')");
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    normalized_contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    sex = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    objective = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                    table.UniqueConstraint("AK_clients_owner_trainer_id_id", x => new { x.owner_trainer_id, x.id });
                    table.CheckConstraint("ck_clients_sex", "sex IN ('male', 'female')");
                    table.ForeignKey(
                        name: "fk_clients_owner_trainer",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clients_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "durable_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    job_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_durable_jobs", x => x.id);
                    table.CheckConstraint("durable_jobs_attempts_non_negative", "attempts >= 0");
                    table.CheckConstraint("status_check", "status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')");
                    table.ForeignKey(
                        name: "FK_durable_jobs_users_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    muscle_groups = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    equipment = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    difficulty_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    video_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercises_users_owner_trainer_id",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "foods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    protein = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    carbs = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    fats = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    kcal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, computedColumnSql: "protein * 4 + carbs * 4 + fats * 9", stored: true),
                    fiber = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_foods", x => x.id);
                    table.CheckConstraint("ck_foods_nutrients_per_100g", "protein BETWEEN 0 AND 100 AND carbs BETWEEN 0 AND 100 AND fats BETWEEN 0 AND 100 AND protein + carbs + fats <= 100 AND (fiber IS NULL OR fiber >= 0)");
                    table.ForeignKey(
                        name: "fk_foods_owner_trainer",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                    table.CheckConstraint("outbox_attempts_non_negative", "attempts >= 0");
                    table.CheckConstraint("status_check", "status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')");
                    table.ForeignKey(
                        name: "FK_outbox_messages_users_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pack_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    session_count = table.Column<int>(type: "integer", nullable: false),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    duration_days = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pack_types", x => x.id);
                    table.UniqueConstraint("AK_pack_types_owner_trainer_id_id", x => new { x.owner_trainer_id, x.id });
                    table.CheckConstraint("pack_duration_positive", "duration_days IS NULL OR duration_days > 0");
                    table.CheckConstraint("pack_price_non_negative", "price_cents >= 0");
                    table.CheckConstraint("pack_session_count_positive", "session_count > 0");
                    table.ForeignKey(
                        name: "FK_pack_types_users_owner_trainer_id",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    rotated_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_refresh_tokens_rotated_from_id",
                        column: x => x.rotated_from_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    unit_of_measure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    serving_size = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    timing = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    trainer_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplements", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplements_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_supplements_users_owner_trainer_id",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trainer_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: "PT Manager"),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_public_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    primary_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, defaultValue: "#000000"),
                    body_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, defaultValue: "#FFFFFF"),
                    background_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    city = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Europe/Lisbon"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_trainer_settings_users_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trainer_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "ACTIVE"),
                    subscription_tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "FREE"),
                    client_limit = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    current_client_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_exempt_from_billing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stripe_subscription_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    stripe_customer_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_subscriptions", x => x.id);
                    table.CheckConstraint("status_check", "subscription_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'CANCELLED')");
                    table.CheckConstraint("tier_check", "subscription_tier IN ('FREE', 'STARTER', 'PRO')");
                    table.ForeignKey(
                        name: "FK_trainer_subscriptions_users_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checkins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    body_fat_percentage = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    training_adherence_score = table.Column<int>(type: "integer", nullable: true),
                    nutrition_adherence_score = table.Column<int>(type: "integer", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    body_measurements = table.Column<string>(type: "jsonb", nullable: false),
                    feedback = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkins", x => x.id);
                    table.CheckConstraint("checkin_body_fat_range", "body_fat_percentage IS NULL OR body_fat_percentage BETWEEN 0 AND 100");
                    table.CheckConstraint("checkin_date_order", "target_date IS NULL OR target_date >= check_in_date");
                    table.CheckConstraint("checkin_nutrition_adherence_range", "nutrition_adherence_score IS NULL OR nutrition_adherence_score BETWEEN 0 AND 100");
                    table.CheckConstraint("checkin_training_adherence_range", "training_adherence_score IS NULL OR training_adherence_score BETWEEN 0 AND 100");
                    table.CheckConstraint("checkin_weight_positive", "weight_kg IS NULL OR weight_kg > 0");
                    table.ForeignKey(
                        name: "FK_checkins_users_owner_trainer_id",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_checkins_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "initial_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    height_cm = table.Column<int>(type: "integer", nullable: false),
                    body_fat_percentage = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    medical_conditions = table.Column<string>(type: "text", nullable: true),
                    fitness_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    activity_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    goals = table.Column<string>(type: "text", nullable: false),
                    profession = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    body_measurements = table.Column<string>(type: "jsonb", nullable: false),
                    nutrition_intake = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_initial_assessments", x => x.id);
                    table.CheckConstraint("ck_initial_assessments_activity_level", "activity_level IN ('sedentary', 'lightly_active', 'moderately_active', 'very_active', 'extremely_active')");
                    table.CheckConstraint("ck_initial_assessments_body_fat_range", "body_fat_percentage IS NULL OR (body_fat_percentage > 0 AND body_fat_percentage < 100)");
                    table.CheckConstraint("ck_initial_assessments_height_positive", "height_cm > 0");
                    table.CheckConstraint("ck_initial_assessments_weight_positive", "weight_kg > 0");
                    table.ForeignKey(
                        name: "fk_initial_assessments_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_initial_assessments_owner_trainer",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invite_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_invite_tokens_client_tenant",
                        columns: x => new { x.trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invite_tokens_trainer",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    starts_date = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_date = table.Column<DateOnly>(type: "date", nullable: true),
                    kcal_target = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    carbs_target_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    fats_target_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    protein_target_g = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    calculation_snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plans", x => x.id);
                    table.CheckConstraint("ck_meal_plans_date_order", "starts_date <= ends_date");
                    table.CheckConstraint("ck_meal_plans_targets", "kcal_target > 0 AND protein_target_g >= 0 AND carbs_target_g >= 0 AND fats_target_g >= 0 AND abs((protein_target_g * 4 + carbs_target_g * 4 + fats_target_g * 9) - kcal_target) <= 100");
                    table.ForeignKey(
                        name: "fk_meal_plans_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meal_plans_owner_trainer",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    template_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    template_data = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("status_check", "status IN ('pending', 'sent', 'failed', 'bounced')");
                    table.ForeignKey(
                        name: "FK_notifications_users_owner_trainer_id",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    training_modality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    starts_date = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_plans", x => x.id);
                    table.CheckConstraint("date_order", "starts_date <= ends_date");
                    table.ForeignKey(
                        name: "FK_training_plans_users_owner_trainer_id",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_training_plans_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_session_packs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pack_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pack_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    total_sessions = table.Column<int>(type: "integer", nullable: false),
                    sessions_remaining = table.Column<int>(type: "integer", nullable: false),
                    price_cents = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_session_packs", x => x.id);
                    table.UniqueConstraint("AK_client_session_packs_owner_trainer_id_client_id_id", x => new { x.owner_trainer_id, x.client_id, x.id });
                    table.CheckConstraint("pack_expiry_order", "expiry_date IS NULL OR expiry_date >= purchase_date");
                    table.CheckConstraint("pack_sessions_consistent", "total_sessions > 0 AND sessions_remaining <= total_sessions");
                    table.CheckConstraint("pack_snapshot_price_non_negative", "price_cents >= 0");
                    table.CheckConstraint("sessions_remaining_non_negative", "sessions_remaining >= 0");
                    table.ForeignKey(
                        name: "FK_client_session_packs_users_owner_trainer_id",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_client_session_packs_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_client_session_packs_pack_type_tenant",
                        columns: x => new { x.owner_trainer_id, x.pack_type_id },
                        principalTable: "pack_types",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_supplement_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serving_size = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    timing = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    trainer_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_supplement_assignments", x => x.id);
                    table.CheckConstraint("ck_client_supplement_assignments_serving_size", "btrim(serving_size) <> ''");
                    table.CheckConstraint("ck_client_supplement_assignments_timing", "btrim(timing) <> ''");
                    table.ForeignKey(
                        name: "fk_client_supplement_assignments_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_client_supplement_assignments_supplement",
                        column: x => x.supplement_id,
                        principalTable: "supplements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_meals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_meals", x => x.id);
                    table.CheckConstraint("meal_order_positive", "order_number > 0");
                    table.CheckConstraint("meal_type_not_blank", "length(trim(meal_type)) > 0");
                    table.ForeignKey(
                        name: "FK_meal_plan_meals_meal_plans_meal_plan_id",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "training_plan_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    training_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    week_number = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_plan_days", x => x.id);
                    table.CheckConstraint("day_range", "day_of_week >= 0 AND day_of_week <= 6");
                    table.CheckConstraint("week_range", "week_number >= 1 AND week_number <= 52");
                    table.ForeignKey(
                        name: "FK_training_plan_days_training_plans_training_plan_id",
                        column: x => x.training_plan_id,
                        principalTable: "training_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_session_pack_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    session_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status_changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.CheckConstraint("ck_sessions_duration", "duration_minutes > 0");
                    table.CheckConstraint("ck_sessions_status", "status IN ('scheduled', 'completed', 'cancelled_by_client', 'cancelled_by_trainer', 'no_show')");
                    table.ForeignKey(
                        name: "fk_sessions_client_pack_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id, x.client_session_pack_id },
                        principalTable: "client_session_packs",
                        principalColumns: new[] { "owner_trainer_id", "client_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sessions_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sessions_owner_trainer",
                        column: x => x.owner_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_meal_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_grams = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_meal_items", x => x.id);
                    table.CheckConstraint("meal_item_order_positive", "order_number > 0");
                    table.CheckConstraint("positive_quantity", "quantity_grams > 0");
                    table.ForeignKey(
                        name: "FK_meal_plan_meal_items_foods_food_id",
                        column: x => x.food_id,
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meal_plan_meal_items_meal_plan_meals_meal_plan_meal_id",
                        column: x => x.meal_plan_meal_id,
                        principalTable: "meal_plan_meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_meal_supplements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_meal_supplements", x => x.id);
                    table.CheckConstraint("meal_supplement_order_positive", "order_number > 0");
                    table.CheckConstraint("positive_supplement_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_meal_plan_meal_supplements_meal_plan_meals_meal_plan_meal_id",
                        column: x => x.meal_plan_meal_id,
                        principalTable: "meal_plan_meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meal_plan_meal_supplements_supplements_supplement_id",
                        column: x => x.supplement_id,
                        principalTable: "supplements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_plan_day_exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    training_plan_day_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    exercise_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_position = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_plan_day_exercises", x => x.id);
                    table.CheckConstraint("day_exercise_group_consistency", "(exercise_group_id IS NULL AND group_position IS NULL) OR (exercise_group_id IS NOT NULL AND group_position > 0)");
                    table.CheckConstraint("day_exercise_order_positive", "order_number > 0");
                    table.ForeignKey(
                        name: "FK_training_plan_day_exercises_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_plan_day_exercises_training_plan_days_training_pla~",
                        column: x => x.training_plan_day_id,
                        principalTable: "training_plan_days",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_exercise_set_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    training_plan_day_exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_number = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    reps_done = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_exercise_set_logs", x => x.id);
                    table.CheckConstraint("reps_check", "reps_done >= 0 AND reps_done <= 100");
                    table.CheckConstraint("set_num_check", "set_number >= 1 AND set_number <= 15");
                    table.CheckConstraint("weight_check", "weight_kg >= 0");
                    table.ForeignKey(
                        name: "FK_client_exercise_set_logs_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_exercise_set_logs_training_plan_day_exercises_traini~",
                        column: x => x.training_plan_day_exercise_id,
                        principalTable: "training_plan_day_exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exercise_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    training_plan_day_exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_number = table.Column<int>(type: "integer", nullable: false),
                    planned_reps = table.Column<int>(type: "integer", nullable: true),
                    planned_weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    rest_seconds_min = table.Column<int>(type: "integer", nullable: true),
                    rest_seconds_max = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_sets", x => x.id);
                    table.CheckConstraint("planned_weight_check", "planned_weight_kg IS NULL OR planned_weight_kg >= 0");
                    table.CheckConstraint("reps_check", "planned_reps IS NULL OR planned_reps > 0");
                    table.CheckConstraint("rest_max_check", "rest_seconds_max IS NULL OR rest_seconds_max >= 0");
                    table.CheckConstraint("rest_min_check", "rest_seconds_min IS NULL OR rest_seconds_min >= 0");
                    table.CheckConstraint("rest_range_check", "rest_seconds_min IS NULL OR rest_seconds_max IS NULL OR rest_seconds_min <= rest_seconds_max");
                    table.CheckConstraint("set_num_check", "set_number >= 1 AND set_number <= 15");
                    table.ForeignKey(
                        name: "FK_exercise_sets_training_plan_day_exercises_training_plan_day~",
                        column: x => x.training_plan_day_exercise_id,
                        principalTable: "training_plan_day_exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_checkins_client",
                table: "checkins",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "idx_checkins_date",
                table: "checkins",
                column: "check_in_date");

            migrationBuilder.CreateIndex(
                name: "idx_checkins_trainer",
                table: "checkins",
                column: "owner_trainer_id");

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

            migrationBuilder.CreateIndex(
                name: "idx_logs_client",
                table: "client_exercise_set_logs",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "idx_logs_exercise",
                table: "client_exercise_set_logs",
                column: "training_plan_day_exercise_id");

            migrationBuilder.CreateIndex(
                name: "unique_set_log",
                table: "client_exercise_set_logs",
                columns: new[] { "client_id", "training_plan_day_exercise_id", "set_number" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_client_session_packs_owner_trainer_id_pack_type_id",
                table: "client_session_packs",
                columns: new[] { "owner_trainer_id", "pack_type_id" });

            migrationBuilder.CreateIndex(
                name: "idx_client_supplement_assignments_supplement",
                table: "client_supplement_assignments",
                column: "supplement_id");

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
                name: "idx_clients_owner_trainer",
                table: "clients",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "uq_clients_tenant_contact_email_active",
                table: "clients",
                columns: new[] { "owner_trainer_id", "normalized_contact_email" },
                unique: true,
                filter: "normalized_contact_email IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_clients_tenant_id",
                table: "clients",
                columns: new[] { "owner_trainer_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_clients_tenant_phone_active",
                table: "clients",
                columns: new[] { "owner_trainer_id", "phone" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_clients_user",
                table: "clients",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_jobs_first_attempt",
                table: "durable_jobs",
                column: "scheduled_at",
                filter: "status = 'pending' AND next_attempt_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_jobs_lease",
                table: "durable_jobs",
                column: "lease_expires_at",
                filter: "status = 'processing'");

            migrationBuilder.CreateIndex(
                name: "idx_jobs_retry",
                table: "durable_jobs",
                column: "next_attempt_at",
                filter: "status = 'pending' AND next_attempt_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_jobs_trainer",
                table: "durable_jobs",
                column: "trainer_id");

            migrationBuilder.CreateIndex(
                name: "unique_idempotency_key",
                table: "durable_jobs",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_sets_exercise",
                table: "exercise_sets",
                column: "training_plan_day_exercise_id");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_name",
                table: "exercises",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_search",
                table: "exercises",
                columns: new[] { "name", "description" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "portuguese");

            migrationBuilder.CreateIndex(
                name: "idx_exercises_trainer",
                table: "exercises",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "idx_foods_owner_name",
                table: "foods",
                columns: new[] { "owner_trainer_id", "name" });

            migrationBuilder.CreateIndex(
                name: "idx_foods_search",
                table: "foods",
                columns: new[] { "name", "description" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "portuguese");

            migrationBuilder.CreateIndex(
                name: "idx_assessments_trainer",
                table: "initial_assessments",
                column: "owner_trainer_id");

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
                name: "idx_invite_tokens_client_expiry",
                table: "invite_tokens",
                columns: new[] { "trainer_id", "client_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "idx_invite_tokens_trainer_email",
                table: "invite_tokens",
                columns: new[] { "trainer_id", "email" });

            migrationBuilder.CreateIndex(
                name: "uq_invite_tokens_hash",
                table: "invite_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_items_meal",
                table: "meal_plan_meal_items",
                column: "meal_plan_meal_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_meal_items_food_id",
                table: "meal_plan_meal_items",
                column: "food_id");

            migrationBuilder.CreateIndex(
                name: "idx_supp_meal",
                table: "meal_plan_meal_supplements",
                column: "meal_plan_meal_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_meal_supplements_supplement_id",
                table: "meal_plan_meal_supplements",
                column: "supplement_id");

            migrationBuilder.CreateIndex(
                name: "unique_supplement_per_meal",
                table: "meal_plan_meal_supplements",
                columns: new[] { "meal_plan_meal_id", "supplement_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_meals_plan",
                table: "meal_plan_meals",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "unique_meal_order",
                table: "meal_plan_meals",
                columns: new[] { "meal_plan_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_meal_plans_client",
                table: "meal_plans",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "idx_meal_plans_trainer",
                table: "meal_plans",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "idx_meal_plans_trainer_active",
                table: "meal_plans",
                columns: new[] { "owner_trainer_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_owner_trainer_id_client_id",
                table: "meal_plans",
                columns: new[] { "owner_trainer_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "idx_notifications_created",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_status",
                table: "notifications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_trainer",
                table: "notifications",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_owner_trainer_id_client_id",
                table: "notifications",
                columns: new[] { "owner_trainer_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "idx_outbox_first_attempt",
                table: "outbox_messages",
                column: "created_at",
                filter: "status = 'pending' AND next_attempt_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_lease",
                table: "outbox_messages",
                column: "lease_expires_at",
                filter: "status = 'processing'");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_retry",
                table: "outbox_messages",
                column: "next_attempt_at",
                filter: "status = 'pending' AND next_attempt_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_trainer",
                table: "outbox_messages",
                column: "trainer_id");

            migrationBuilder.CreateIndex(
                name: "unique_outbox_idempotency_key",
                table: "outbox_messages",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pack_types_usable",
                table: "pack_types",
                columns: new[] { "owner_trainer_id", "name" },
                filter: "is_active = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_packs_trainer",
                table: "pack_types",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "uq_pack_types_tenant_id",
                table: "pack_types",
                columns: new[] { "owner_trainer_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_stripe_events_type",
                table: "processed_stripe_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_processed_stripe_events_stripe_event_id",
                table: "processed_stripe_events",
                column: "stripe_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_expires",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_family",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_user",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_rotated_from_id",
                table: "refresh_tokens",
                column: "rotated_from_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_sessions_client_session_pack",
                table: "sessions",
                column: "client_session_pack_id",
                filter: "client_session_pack_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_tenant_client_starts_at",
                table: "sessions",
                columns: new[] { "owner_trainer_id", "client_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "idx_sessions_tenant_scheduled_at",
                table: "sessions",
                columns: new[] { "owner_trainer_id", "starts_at" },
                filter: "status = 'scheduled' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_owner_trainer_id_client_id_client_session_pack_id",
                table: "sessions",
                columns: new[] { "owner_trainer_id", "client_id", "client_session_pack_id" });

            migrationBuilder.CreateIndex(
                name: "idx_supplements_name",
                table: "supplements",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_supplements_trainer",
                table: "supplements",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplements_created_by_user_id",
                table: "supplements",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_settings_trainer",
                table: "trainer_settings",
                column: "trainer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_subscriptions_status",
                table: "trainer_subscriptions",
                column: "subscription_status");

            migrationBuilder.CreateIndex(
                name: "uq_trainer_subscriptions_trainer",
                table: "trainer_subscriptions",
                column: "trainer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_day_exercises_day",
                table: "training_plan_day_exercises",
                column: "training_plan_day_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_plan_day_exercises_exercise_id",
                table: "training_plan_day_exercises",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "uq_day_exercise_group_position",
                table: "training_plan_day_exercises",
                columns: new[] { "training_plan_day_id", "exercise_group_id", "group_position" },
                unique: true,
                filter: "exercise_group_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_day_exercise_isolated_order",
                table: "training_plan_day_exercises",
                columns: new[] { "training_plan_day_id", "order_number" },
                unique: true,
                filter: "exercise_group_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_days_plan",
                table: "training_plan_days",
                column: "training_plan_id");

            migrationBuilder.CreateIndex(
                name: "idx_training_plans_client",
                table: "training_plans",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "idx_training_plans_trainer",
                table: "training_plans",
                column: "owner_trainer_id");

            migrationBuilder.CreateIndex(
                name: "idx_training_plans_trainer_active",
                table: "training_plans",
                columns: new[] { "owner_trainer_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_training_plans_owner_trainer_id_client_id",
                table: "training_plans",
                columns: new[] { "owner_trainer_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "idx_users_role",
                table: "users",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "uq_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkins");

            migrationBuilder.DropTable(
                name: "client_exercise_set_logs");

            migrationBuilder.DropTable(
                name: "client_supplement_assignments");

            migrationBuilder.DropTable(
                name: "durable_jobs");

            migrationBuilder.DropTable(
                name: "exercise_sets");

            migrationBuilder.DropTable(
                name: "initial_assessments");

            migrationBuilder.DropTable(
                name: "invite_tokens");

            migrationBuilder.DropTable(
                name: "meal_plan_meal_items");

            migrationBuilder.DropTable(
                name: "meal_plan_meal_supplements");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "processed_stripe_events");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "trainer_settings");

            migrationBuilder.DropTable(
                name: "trainer_subscriptions");

            migrationBuilder.DropTable(
                name: "training_plan_day_exercises");

            migrationBuilder.DropTable(
                name: "foods");

            migrationBuilder.DropTable(
                name: "meal_plan_meals");

            migrationBuilder.DropTable(
                name: "supplements");

            migrationBuilder.DropTable(
                name: "client_session_packs");

            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "training_plan_days");

            migrationBuilder.DropTable(
                name: "meal_plans");

            migrationBuilder.DropTable(
                name: "pack_types");

            migrationBuilder.DropTable(
                name: "training_plans");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
