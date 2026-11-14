using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengePracticeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Challenges",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPoints",
                table: "Challenges",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<string>(
                name: "Advice",
                table: "ChallengeEvaluations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPassed",
                table: "ChallengeEvaluations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PointsAwarded",
                table: "ChallengeEvaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedCode",
                table: "ChallengeAttempts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "MaxPoints",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "Advice",
                table: "ChallengeEvaluations");

            migrationBuilder.DropColumn(
                name: "IsPassed",
                table: "ChallengeEvaluations");

            migrationBuilder.DropColumn(
                name: "PointsAwarded",
                table: "ChallengeEvaluations");

            migrationBuilder.DropColumn(
                name: "SubmittedCode",
                table: "ChallengeAttempts");
        }
    }
}
