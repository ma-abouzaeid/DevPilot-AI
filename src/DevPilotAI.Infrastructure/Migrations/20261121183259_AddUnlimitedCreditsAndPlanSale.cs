using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnlimitedCreditsAndPlanSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasUnlimitedCredits",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SaleDiscountPercent",
                table: "Plans",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SaleEnabled",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "PlanId",
                keyValue: 1,
                columns: new[] { "SaleDiscountPercent", "SaleEnabled" },
                values: new object[] { 0m, false });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "PlanId",
                keyValue: 2,
                columns: new[] { "SaleDiscountPercent", "SaleEnabled" },
                values: new object[] { 0m, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasUnlimitedCredits",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SaleDiscountPercent",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "SaleEnabled",
                table: "Plans");
        }
    }
}
