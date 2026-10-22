using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSelectedMealPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "selected_meal_plan_id",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_clients_selected_meal_plan",
                table: "clients",
                column: "selected_meal_plan_id",
                filter: "selected_meal_plan_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_clients_selected_meal_plan",
                table: "clients",
                column: "selected_meal_plan_id",
                principalTable: "meal_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_clients_selected_meal_plan",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "idx_clients_selected_meal_plan",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "selected_meal_plan_id",
                table: "clients");
        }
    }
}
