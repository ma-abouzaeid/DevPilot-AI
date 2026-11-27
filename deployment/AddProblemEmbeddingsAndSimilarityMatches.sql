-- Similarity tables (migration 20261106123513).
-- Use on MonsterASP if Analyze AI returns 500 and DebuggingAttempts stay empty.
-- Safe to re-run.

IF OBJECT_ID(N'dbo.ProblemEmbeddings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProblemEmbeddings] (
        [EmbeddingId] BIGINT IDENTITY(1,1) NOT NULL,
        [ProblemRequestId] INT NOT NULL,
        [EmbeddingData] VARBINARY(MAX) NOT NULL,
        [ModelVersion] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_ProblemEmbeddings] PRIMARY KEY ([EmbeddingId]),
        CONSTRAINT [FK_ProblemEmbeddings_ProblemRequests_ProblemRequestId]
            FOREIGN KEY ([ProblemRequestId]) REFERENCES [dbo].[ProblemRequests] ([ProblemRequestId])
    );

    CREATE UNIQUE INDEX [IX_ProblemEmbeddings_ProblemRequestId]
        ON [dbo].[ProblemEmbeddings] ([ProblemRequestId]);
END
GO

IF OBJECT_ID(N'dbo.SimilarityMatches', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SimilarityMatches] (
        [SimilarityMatchId] BIGINT IDENTITY(1,1) NOT NULL,
        [ProblemRequestId] INT NOT NULL,
        [MatchedProblemId] INT NOT NULL,
        [SimilarityScore] DECIMAL(6,5) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_SimilarityMatches] PRIMARY KEY ([SimilarityMatchId]),
        CONSTRAINT [FK_SimilarityMatches_ProblemRequests_ProblemRequestId]
            FOREIGN KEY ([ProblemRequestId]) REFERENCES [dbo].[ProblemRequests] ([ProblemRequestId]),
        CONSTRAINT [FK_SimilarityMatches_Problems_MatchedProblemId]
            FOREIGN KEY ([MatchedProblemId]) REFERENCES [dbo].[Problems] ([ProblemId])
    );

    CREATE INDEX [IX_SimilarityMatches_MatchedProblemId]
        ON [dbo].[SimilarityMatches] ([MatchedProblemId]);

    CREATE INDEX [IX_SimilarityMatches_ProblemRequestId]
        ON [dbo].[SimilarityMatches] ([ProblemRequestId]);
END
GO

-- Record the EF migration so future Migrate() calls skip it.
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20261106123513_AddProblemEmbeddingsAndSimilarityMatches'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106123513_AddProblemEmbeddingsAndSimilarityMatches', N'9.0.2');
END
GO
