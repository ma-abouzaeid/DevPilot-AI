using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditActionCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditActionCosts",
                columns: table => new
                {
                    ActionKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    ShowOnPlansPage = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditActionCosts", x => x.ActionKey);
                });

            migrationBuilder.InsertData(
                table: "CreditActionCosts",
                columns: new[] { "ActionKey", "Credits", "DisplayName", "ShowOnPlansPage", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { "ChallengeEvaluation", 5, "Challenge evaluate", true, 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "ChallengeGeneration", 3, "Challenge generation", false, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CodeReview", 15, "Code Review", true, 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Debugging", 5, "AI Debug", true, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "KnowledgeAdaptation", 2, "Knowledge reuse", true, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditActionCosts_SortOrder",
                table: "CreditActionCosts",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditActionCosts");
        }
    }
}
