using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    public partial class AddSoftDeleteEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ProblemSolutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProblemSolutions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Problems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Problems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ProblemAttachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProblemAttachments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "KnowledgeArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "KnowledgeArticles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "GitHubRepositories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "GitHubRepositories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CodeReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CodeReviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Challenges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Challenges",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProblemSolutions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProblemSolutions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProblemAttachments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProblemAttachments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GitHubRepositories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "GitHubRepositories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CodeReviews");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CodeReviews");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Challenges");
        }
    }
}
