using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeBillingOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "client_limit",
                table: "trainer_subscriptions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 5);

            migrationBuilder.Sql(
                """
                UPDATE trainer_subscriptions
                SET client_limit = CASE subscription_tier
                    WHEN 'FREE' THEN 5
                    WHEN 'STARTER' THEN 25
                    WHEN 'PRO' THEN NULL
                END
                WHERE subscription_tier IN ('FREE', 'STARTER', 'PRO');
                """);

            migrationBuilder.CreateTable(
                name: "billing_checkout_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    lease_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stripe_checkout_session_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    stripe_session_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_checkout_operations", x => x.id);
                    table.CheckConstraint("ck_billing_checkout_operations_lease", "(status = 'pending' AND lease_owner_id IS NOT NULL AND lease_expires_at IS NOT NULL) OR (status <> 'pending' AND lease_owner_id IS NULL AND lease_expires_at IS NULL)");
                    table.CheckConstraint("ck_billing_checkout_operations_status", "status IN ('pending', 'created', 'completed', 'expired', 'failed')");
                    table.ForeignKey(
                        name: "FK_billing_checkout_operations_users_trainer_id",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_trainer_subscriptions_tier_client_limit",
                table: "trainer_subscriptions",
                sql: "(subscription_tier = 'FREE' AND client_limit = 5) OR (subscription_tier = 'STARTER' AND client_limit = 25) OR (subscription_tier = 'PRO' AND client_limit IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_billing_checkout_operations_status_lease_expires_at",
                table: "billing_checkout_operations",
                columns: new[] { "status", "lease_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_billing_checkout_operations_stripe_session",
                table: "billing_checkout_operations",
                column: "stripe_checkout_session_id",
                filter: "stripe_checkout_session_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_billing_checkout_operations_active_trainer",
                table: "billing_checkout_operations",
                column: "trainer_id",
                unique: true,
                filter: "status IN ('pending', 'created')");

            migrationBuilder.CreateIndex(
                name: "uq_billing_checkout_operations_trainer_client_operation",
                table: "billing_checkout_operations",
                columns: new[] { "trainer_id", "client_operation_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_checkout_operations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trainer_subscriptions_tier_client_limit",
                table: "trainer_subscriptions");

            migrationBuilder.Sql(
                """
                DO $preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM trainer_subscriptions
                        WHERE client_limit IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Rollback blocked: unlimited PRO subscriptions require an explicit downgrade.';
                    END IF;
                END
                $preflight$;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "client_limit",
                table: "trainer_subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
