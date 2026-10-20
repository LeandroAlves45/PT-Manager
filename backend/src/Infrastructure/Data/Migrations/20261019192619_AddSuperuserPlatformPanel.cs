using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperuserPlatformPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_supplements_timing",
                table: "supplements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_client_supplement_assignments_timing",
                table: "client_supplement_assignments");

            migrationBuilder.AddColumn<DateTime>(
                name: "billing_exempt_since",
                table: "trainer_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "timing",
                table: "supplements",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "timing",
                table: "client_supplement_assignments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateTable(
                name: "platform_subscription_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_invoice_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount_paid_cents = table.Column<long>(type: "bigint", nullable: false),
                    net_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_subscription_payments", x => x.id);
                    table.CheckConstraint("ck_platform_subscription_payments_amount", "amount_paid_cents > 0 AND net_cents <= amount_paid_cents");
                    table.CheckConstraint("ck_platform_subscription_payments_currency", "char_length(currency) = 3");
                    table.ForeignKey(
                        name: "fk_platform_subscription_payments_trainer",
                        column: x => x.trainer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trainer_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_trainer_invitations_creator",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplements_timing",
                table: "supplements",
                sql: "timing IS NULL OR btrim(timing) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_client_supplement_assignments_timing",
                table: "client_supplement_assignments",
                sql: "timing IS NULL OR btrim(timing) <> ''");

            migrationBuilder.CreateIndex(
                name: "idx_platform_subscription_payments_paid_at",
                table: "platform_subscription_payments",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "IX_platform_subscription_payments_trainer_id",
                table: "platform_subscription_payments",
                column: "trainer_id");

            migrationBuilder.CreateIndex(
                name: "uq_platform_subscription_payments_invoice",
                table: "platform_subscription_payments",
                column: "stripe_invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_invitations_created_by_user_id",
                table: "trainer_invitations",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_trainer_invitations_hash",
                table: "trainer_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_trainer_invitations_pending_email",
                table: "trainer_invitations",
                column: "normalized_email",
                unique: true,
                filter: "used_at IS NULL AND revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_subscription_payments");

            migrationBuilder.DropTable(
                name: "trainer_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_supplements_timing",
                table: "supplements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_client_supplement_assignments_timing",
                table: "client_supplement_assignments");

            migrationBuilder.DropColumn(
                name: "billing_exempt_since",
                table: "trainer_subscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "timing",
                table: "supplements",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "timing",
                table: "client_supplement_assignments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_supplements_timing",
                table: "supplements",
                sql: "btrim(timing) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_client_supplement_assignments_timing",
                table: "client_supplement_assignments",
                sql: "btrim(timing) <> ''");
        }
    }
}
