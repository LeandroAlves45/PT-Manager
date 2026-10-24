using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanSupplementsAndPlanIntakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meal_plan_supplements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_supplements", x => x.id);
                    table.CheckConstraint("ck_meal_plan_supplements_order_positive", "order_number > 0");
                    table.CheckConstraint("ck_meal_plan_supplements_quantity_positive", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_meal_plan_supplements_meal_plan",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meal_plan_supplements_supplement",
                        column: x => x.supplement_id,
                        principalTable: "supplements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_meal_plan_supplement_intakes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_trainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_meal_supplement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    meal_plan_supplement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    taken_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_meal_plan_supplement_intakes", x => x.id);
                    table.CheckConstraint("ck_client_meal_plan_supplement_intakes_single_reference", "num_nonnulls(meal_plan_meal_supplement_id, meal_plan_supplement_id) <= 1");
                    table.ForeignKey(
                        name: "fk_client_meal_plan_supplement_intakes_client_tenant",
                        columns: x => new { x.owner_trainer_id, x.client_id },
                        principalTable: "clients",
                        principalColumns: new[] { "owner_trainer_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_client_meal_plan_supplement_intakes_meal_plan",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_client_meal_plan_supplement_intakes_meal_supplement",
                        column: x => x.meal_plan_meal_supplement_id,
                        principalTable: "meal_plan_meal_supplements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_client_meal_plan_supplement_intakes_plan_supplement",
                        column: x => x.meal_plan_supplement_id,
                        principalTable: "meal_plan_supplements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_client_meal_plan_supplement_intakes_supplement",
                        column: x => x.supplement_id,
                        principalTable: "supplements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_client_meal_plan_supplement_intakes_client_plan_date",
                table: "client_meal_plan_supplement_intakes",
                columns: new[] { "owner_trainer_id", "client_id", "meal_plan_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "IX_client_meal_plan_supplement_intakes_meal_plan_id",
                table: "client_meal_plan_supplement_intakes",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_meal_plan_supplement_intakes_supplement_id",
                table: "client_meal_plan_supplement_intakes",
                column: "supplement_id");

            migrationBuilder.CreateIndex(
                name: "uq_client_meal_plan_supplement_intakes_meal_supplement_date",
                table: "client_meal_plan_supplement_intakes",
                columns: new[] { "meal_plan_meal_supplement_id", "local_date" },
                unique: true,
                filter: "meal_plan_meal_supplement_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_client_meal_plan_supplement_intakes_plan_supplement_date",
                table: "client_meal_plan_supplement_intakes",
                columns: new[] { "meal_plan_supplement_id", "local_date" },
                unique: true,
                filter: "meal_plan_supplement_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_supplements_supplement_id",
                table: "meal_plan_supplements",
                column: "supplement_id");

            migrationBuilder.CreateIndex(
                name: "uq_meal_plan_supplements_plan_supplement",
                table: "meal_plan_supplements",
                columns: new[] { "meal_plan_id", "supplement_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_meal_plan_supplement_intakes");

            migrationBuilder.DropTable(
                name: "meal_plan_supplements");
        }
    }
}
