using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFeedbackRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Rating",
                table: "UserFeedbacks",
                type: "tinyint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "UserFeedbacks");
        }
    }
}
