using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleComments",
                columns: table => new
                {
                    ArticleCommentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KnowledgeArticleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleComments", x => x.ArticleCommentId);
                    table.ForeignKey(
                        name: "FK_ArticleComments_KnowledgeArticles_KnowledgeArticleId",
                        column: x => x.KnowledgeArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "KnowledgeArticleId");
                    table.ForeignKey(
                        name: "FK_ArticleComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ArticleLikes",
                columns: table => new
                {
                    ArticleLikeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KnowledgeArticleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleLikes", x => x.ArticleLikeId);
                    table.ForeignKey(
                        name: "FK_ArticleLikes_KnowledgeArticles_KnowledgeArticleId",
                        column: x => x.KnowledgeArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "KnowledgeArticleId");
                    table.ForeignKey(
                        name: "FK_ArticleLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ArticleSaves",
                columns: table => new
                {
                    ArticleSaveId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KnowledgeArticleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleSaves", x => x.ArticleSaveId);
                    table.ForeignKey(
                        name: "FK_ArticleSaves_KnowledgeArticles_KnowledgeArticleId",
                        column: x => x.KnowledgeArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "KnowledgeArticleId");
                    table.ForeignKey(
                        name: "FK_ArticleSaves_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            // May already exist from deployment/AddSystemErrorLogs.sql — create only if missing.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[SystemErrorLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SystemErrorLogs] (
        [SystemErrorLogId] BIGINT IDENTITY(1,1) NOT NULL,
        [TraceId] NVARCHAR(100) NOT NULL,
        [HttpMethod] NVARCHAR(20) NOT NULL,
        [Path] NVARCHAR(500) NOT NULL,
        [QueryString] NVARCHAR(1000) NULL,
        [Action] NVARCHAR(150) NOT NULL,
        [StatusCode] INT NOT NULL,
        [ErrorCode] NVARCHAR(100) NOT NULL,
        [Message] NVARCHAR(2000) NOT NULL,
        [ExceptionType] NVARCHAR(300) NULL,
        [ExceptionMessage] NVARCHAR(4000) NULL,
        [StackTrace] NVARCHAR(MAX) NULL,
        [ResponseJson] NVARCHAR(MAX) NULL,
        [UserId] INT NULL,
        [IpAddress] NVARCHAR(100) NULL,
        [UserAgent] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_SystemErrorLogs] PRIMARY KEY ([SystemErrorLogId]),
        CONSTRAINT [FK_SystemErrorLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId])
    );
    CREATE INDEX [IX_SystemErrorLogs_CreatedAt] ON [dbo].[SystemErrorLogs] ([CreatedAt]);
    CREATE INDEX [IX_SystemErrorLogs_ErrorCode] ON [dbo].[SystemErrorLogs] ([ErrorCode]);
    CREATE INDEX [IX_SystemErrorLogs_StatusCode] ON [dbo].[SystemErrorLogs] ([StatusCode]);
    CREATE INDEX [IX_SystemErrorLogs_TraceId] ON [dbo].[SystemErrorLogs] ([TraceId]);
    CREATE INDEX [IX_SystemErrorLogs_UserId] ON [dbo].[SystemErrorLogs] ([UserId]);
END
");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleComments_CreatedAt",
                table: "ArticleComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleComments_KnowledgeArticleId",
                table: "ArticleComments",
                column: "KnowledgeArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleComments_UserId",
                table: "ArticleComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLikes_KnowledgeArticleId_UserId",
                table: "ArticleLikes",
                columns: new[] { "KnowledgeArticleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLikes_UserId",
                table: "ArticleLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleSaves_KnowledgeArticleId_UserId",
                table: "ArticleSaves",
                columns: new[] { "KnowledgeArticleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleSaves_UserId",
                table: "ArticleSaves",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleComments");

            migrationBuilder.DropTable(
                name: "ArticleLikes");

            migrationBuilder.DropTable(
                name: "ArticleSaves");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[SystemErrorLogs]', N'U') IS NOT NULL
    DROP TABLE [dbo].[SystemErrorLogs];
");
        }
    }
}
