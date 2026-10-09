using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace DevPilotAI.Infrastructure.Migrations
{
    public partial class AddPlanAndUsageEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIUsageEvents",
                columns: table => new
                {
                    AIUsageEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AIRequestId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProblemId = table.Column<int>(type: "int", nullable: true),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: true),
                    OperationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CostType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreditsReserved = table.Column<int>(type: "int", nullable: true),
                    CreditsCharged = table.Column<int>(type: "int", nullable: true),
                    CreditsRefunded = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    RetrySequence = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIUsageEvents", x => x.AIUsageEventId);
                    table.ForeignKey(
                        name: "FK_AIUsageEvents_AIModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "AIModels",
                        principalColumn: "AIModelId");
                    table.ForeignKey(
                        name: "FK_AIUsageEvents_AIProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "AIProviderId");
                    table.ForeignKey(
                        name: "FK_AIUsageEvents_AIRequests_AIRequestId",
                        column: x => x.AIRequestId,
                        principalTable: "AIRequests",
                        principalColumn: "AIRequestId");
                    table.ForeignKey(
                        name: "FK_AIUsageEvents_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "ProblemId");
                    table.ForeignKey(
                        name: "FK_AIUsageEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Features",
                columns: table => new
                {
                    FeatureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.FeatureId);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreditsPerPeriod = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.PlanId);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatures",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    FeatureId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LimitValue = table.Column<int>(type: "int", nullable: true),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatures", x => new { x.PlanId, x.FeatureId });
                    table.ForeignKey(
                        name: "FK_PlanFeatures_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId");
                    table.ForeignKey(
                        name: "FK_PlanFeatures_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "PlanId");
                });

            migrationBuilder.CreateTable(
                name: "UsagePeriods",
                columns: table => new
                {
                    UsagePeriodId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreditsAllocated = table.Column<int>(type: "int", nullable: false),
                    CreditsReserved = table.Column<int>(type: "int", nullable: false),
                    CreditsUsed = table.Column<int>(type: "int", nullable: false),
                    CreditsRefunded = table.Column<int>(type: "int", nullable: false),
                    RequestCount = table.Column<long>(type: "bigint", nullable: false),
                    TokenCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsagePeriods", x => x.UsagePeriodId);
                    table.ForeignKey(
                        name: "FK_UsagePeriods_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "PlanId");
                    table.ForeignKey(
                        name: "FK_UsagePeriods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: true),
                    AssignmentSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.SubscriptionId);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "PlanId");
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UsageLedgers",
                columns: table => new
                {
                    UsageLedgerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    UsagePeriodId = table.Column<long>(type: "bigint", nullable: false),
                    AIUsageEventId = table.Column<long>(type: "bigint", nullable: true),
                    EntryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageLedgers", x => x.UsageLedgerId);
                    table.ForeignKey(
                        name: "FK_UsageLedgers_AIUsageEvents_AIUsageEventId",
                        column: x => x.AIUsageEventId,
                        principalTable: "AIUsageEvents",
                        principalColumn: "AIUsageEventId");
                    table.ForeignKey(
                        name: "FK_UsageLedgers_UsagePeriods_UsagePeriodId",
                        column: x => x.UsagePeriodId,
                        principalTable: "UsagePeriods",
                        principalColumn: "UsagePeriodId");
                    table.ForeignKey(
                        name: "FK_UsageLedgers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.InsertData(
                table: "Features",
                columns: new[] { "FeatureId", "Category", "CreatedAt", "Description", "Key", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "AI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Access to AI debugging assistant.", "ai_debugging", "AI Debugging", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Quota", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Total AI credits available per billing cycle.", "monthly_ai_credits", "Monthly AI Credits", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "PlanId", "Code", "CreatedAt", "CreditsPerPeriod", "Currency", "Description", "IsDefault", "IsPublic", "Name", "PeriodType", "Price", "Status", "UpdatedAt" },
                values: new object[] { 1, "FREE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 100, "USD", "Default Free tier with standard AI quota.", true, true, "Free", "Monthly", 0.00m, "Active", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "PlanFeatures",
                columns: new[] { "FeatureId", "PlanId", "ConfigJson", "CreatedAt", "IsEnabled", "LimitValue", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 1, "{\"enabled\": true}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 100, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 1, "{\"credits\": 100}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 100, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageEvents_AIRequestId",
                table: "AIUsageEvents",
                column: "AIRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageEvents_CreatedAt",
                table: "AIUsageEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageEvents_ModelId",
                table: "AIUsageEvents",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageEvents_OperationType",
                table: "AIUsageEvents",
                column: "OperationType");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageEvents_ProblemId",
                table: "AIUsageEvents",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageEvents_ProviderId",
                table: "AIUsageEvents",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageEvents_UserId",
                table: "AIUsageEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Features_Key",
                table: "Features",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_FeatureId",
                table: "PlanFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Code",
                table: "Plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgers_AIUsageEventId",
                table: "UsageLedgers",
                column: "AIUsageEventId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgers_CreatedAt",
                table: "UsageLedgers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgers_UsagePeriodId",
                table: "UsageLedgers",
                column: "UsagePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgers_UserId",
                table: "UsageLedgers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsagePeriods_PlanId",
                table: "UsagePeriods",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UsagePeriods_UserId",
                table: "UsagePeriods",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsagePeriods_UserId_PeriodStart_PeriodEnd",
                table: "UsagePeriods",
                columns: new[] { "UserId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_AssignedByUserId",
                table: "UserSubscriptions",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PlanId",
                table: "UserSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_Status",
                table: "UserSubscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId",
                table: "UserSubscriptions",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanFeatures");

            migrationBuilder.DropTable(
                name: "UsageLedgers");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "Features");

            migrationBuilder.DropTable(
                name: "AIUsageEvents");

            migrationBuilder.DropTable(
                name: "UsagePeriods");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
