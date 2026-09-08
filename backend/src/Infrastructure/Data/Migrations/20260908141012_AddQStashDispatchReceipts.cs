using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQStashDispatchReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "qstash_dispatch_receipts",
                columns: table => new
                {
                    jti_hash = table.Column<string>(type: "character(64)", nullable: false),
                    token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qstash_dispatch_receipts", x => x.jti_hash);
                });

            migrationBuilder.CreateIndex(
                name: "ix_qstash_dispatch_receipts_token_expires_at",
                table: "qstash_dispatch_receipts",
                column: "token_expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qstash_dispatch_receipts");
        }
    }
}
