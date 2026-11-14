using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemEmbeddingsAndSimilarityMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProblemEmbeddings",
                columns: table => new
                {
                    EmbeddingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemRequestId = table.Column<int>(type: "int", nullable: false),
                    EmbeddingData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemEmbeddings", x => x.EmbeddingId);
                    table.ForeignKey(
                        name: "FK_ProblemEmbeddings_ProblemRequests_ProblemRequestId",
                        column: x => x.ProblemRequestId,
                        principalTable: "ProblemRequests",
                        principalColumn: "ProblemRequestId");
                });

            migrationBuilder.CreateTable(
                name: "SimilarityMatches",
                columns: table => new
                {
                    SimilarityMatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemRequestId = table.Column<int>(type: "int", nullable: false),
                    MatchedProblemId = table.Column<int>(type: "int", nullable: false),
                    SimilarityScore = table.Column<decimal>(type: "decimal(6,5)", precision: 6, scale: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimilarityMatches", x => x.SimilarityMatchId);
                    table.ForeignKey(
                        name: "FK_SimilarityMatches_ProblemRequests_ProblemRequestId",
                        column: x => x.ProblemRequestId,
                        principalTable: "ProblemRequests",
                        principalColumn: "ProblemRequestId");
                    table.ForeignKey(
                        name: "FK_SimilarityMatches_Problems_MatchedProblemId",
                        column: x => x.MatchedProblemId,
                        principalTable: "Problems",
                        principalColumn: "ProblemId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemEmbeddings_ProblemRequestId",
                table: "ProblemEmbeddings",
                column: "ProblemRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SimilarityMatches_MatchedProblemId",
                table: "SimilarityMatches",
                column: "MatchedProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_SimilarityMatches_ProblemRequestId",
                table: "SimilarityMatches",
                column: "ProblemRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblemEmbeddings");

            migrationBuilder.DropTable(
                name: "SimilarityMatches");
        }
    }
}
