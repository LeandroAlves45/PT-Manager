using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateTable(
                name: "external_authentication_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nonce_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_authentication_challenges", x => x.id);
                    table.CheckConstraint("ck_external_auth_challenges_actor", "(purpose = 'sign_in' AND user_id IS NULL) OR (purpose = 'link' AND user_id IS NOT NULL)");
                    table.CheckConstraint("ck_external_auth_challenges_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_external_auth_challenges_purpose", "purpose IN ('sign_in', 'link')");
                    table.ForeignKey(
                        name: "FK_external_authentication_challenges_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_identities", x => x.id);
                    table.CheckConstraint("ck_external_identities_provider", "provider IN ('google')");
                    table.ForeignKey(
                        name: "FK_external_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_external_auth_challenges_expires_at",
                table: "external_authentication_challenges",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_external_authentication_challenges_user_id",
                table: "external_authentication_challenges",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_external_auth_challenges_nonce_hash",
                table: "external_authentication_challenges",
                column: "nonce_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_external_identities_provider_subject",
                table: "external_identities",
                columns: new[] { "provider", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_external_identities_user_provider",
                table: "external_identities",
                columns: new[] { "user_id", "provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preflight obrigatório: repor password_hash NOT NULL sobre contas criadas por
            // Google atribuiria a essas contas o hash vazio gerado por defaultValue "",
            // o que é destrutivo e silencioso. A recusa é intencional — inventar passwords
            // ou eliminar utilizadores seriam as únicas alternativas.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM users WHERE password_hash IS NULL) THEN
                        RAISE EXCEPTION 'Cannot rollback AddExternalIdentities while passwordless users exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "external_authentication_challenges");

            migrationBuilder.DropTable(
                name: "external_identities");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
