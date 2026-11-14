using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProPlanAndGitHubCodeReviewFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Features",
                columns: new[] { "FeatureId", "Category", "CreatedAt", "Description", "Key", "Name", "UpdatedAt" },
                values: new object[] { 3, "AI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AI code review directly from connected GitHub repositories.", "github_code_review", "GitHub Code Review", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "PlanId",
                keyValue: 1,
                column: "Description",
                value: "Debug with AI, practice challenges, and publish knowledge. 100 credits / month.");

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "PlanId", "Code", "CreatedAt", "CreditsPerPeriod", "Currency", "Description", "IsDefault", "IsPublic", "Name", "PeriodType", "Price", "Status", "UpdatedAt" },
                values: new object[] { 2, "PRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1000, "USD", "Higher AI credits plus GitHub Code Review for teams that ship faster.", false, true, "Pro", "Monthly", 19.00m, "Active", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "PlanFeatures",
                columns: new[] { "FeatureId", "PlanId", "ConfigJson", "CreatedAt", "IsEnabled", "LimitValue", "UpdatedAt" },
                values: new object[,]
                {
                    { 3, 1, "{\"enabled\": false}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 1, 2, "{\"enabled\": true}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1000, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 2, "{\"credits\": 1000}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1000, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 2, "{\"enabled\": true}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumns: new[] { "FeatureId", "PlanId" },
                keyValues: new object[] { 3, 1 });

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumns: new[] { "FeatureId", "PlanId" },
                keyValues: new object[] { 1, 2 });

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumns: new[] { "FeatureId", "PlanId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumns: new[] { "FeatureId", "PlanId" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "FeatureId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "PlanId",
                keyValue: 2);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "PlanId",
                keyValue: 1,
                column: "Description",
                value: "Default Free tier with standard AI quota.");
        }
    }
}
