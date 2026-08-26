using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSprint3Lote3G : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_clients_user",
                table: "clients");

            migrationBuilder.RenameIndex(
                name: "IX_processed_stripe_events_stripe_event_id",
                table: "processed_stripe_events",
                newName: "uq_processed_stripe_events_event_id");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_provider_state_observed_at",
                table: "trainer_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "invite_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_verification_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_verification_tokens_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_password_reset_tokens_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_transfer_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_transfer_audits", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_transfer_audits_source_trainer",
                        column: x => x.source_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_transfer_audits_target_client_tenant",
                        columns: x => new { x.target_trainer_id, x.target_client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_transfer_audits_target_trainer",
                        column: x => x.target_trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_transfer_audits_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_trainer_subscriptions_stripe_customer",
                table: "trainer_subscriptions",
                column: "stripe_customer_id",
                unique: true,
                filter: "stripe_customer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_trainer_subscriptions_stripe_subscription",
                table: "trainer_subscriptions",
                column: "stripe_subscription_id",
                unique: true,
                filter: "stripe_subscription_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_clients_user_active",
                table: "clients",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL AND is_active = true AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_email_verification_tokens_user_consumed",
                table: "email_verification_tokens",
                columns: new[] { "user_id", "consumed_at" });

            migrationBuilder.CreateIndex(
                name: "uq_email_verification_tokens_hash",
                table: "email_verification_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_password_reset_tokens_user_consumed",
                table: "password_reset_tokens",
                columns: new[] { "user_id", "consumed_at" });

            migrationBuilder.CreateIndex(
                name: "uq_password_reset_tokens_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tenant_transfer_audits_user_occurred",
                table: "tenant_transfer_audits",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_transfer_audits_source_trainer_id",
                table: "tenant_transfer_audits",
                column: "source_trainer_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_transfer_audits_target_trainer_id_target_client_id",
                table: "tenant_transfer_audits",
                columns: new[] { "target_trainer_id", "target_client_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_verification_tokens");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "tenant_transfer_audits");

            migrationBuilder.DropIndex(
                name: "uq_trainer_subscriptions_stripe_customer",
                table: "trainer_subscriptions");

            migrationBuilder.DropIndex(
                name: "uq_trainer_subscriptions_stripe_subscription",
                table: "trainer_subscriptions");

            migrationBuilder.DropIndex(
                name: "uq_clients_user_active",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "last_provider_state_observed_at",
                table: "trainer_subscriptions");

            migrationBuilder.RenameIndex(
                name: "uq_processed_stripe_events_event_id",
                table: "processed_stripe_events",
                newName: "IX_processed_stripe_events_stripe_event_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "invite_tokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "uq_clients_user",
                table: "clients",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");
        }
    }
}
