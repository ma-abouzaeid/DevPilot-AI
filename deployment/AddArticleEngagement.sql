IF OBJECT_ID(N'[dbo].[ArticleLikes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ArticleLikes] (
        [ArticleLikeId] BIGINT IDENTITY(1,1) NOT NULL,
        [KnowledgeArticleId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_ArticleLikes] PRIMARY KEY ([ArticleLikeId]),
        CONSTRAINT [FK_ArticleLikes_KnowledgeArticles_KnowledgeArticleId]
            FOREIGN KEY ([KnowledgeArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([KnowledgeArticleId]),
        CONSTRAINT [FK_ArticleLikes_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId])
    );

    CREATE UNIQUE INDEX [IX_ArticleLikes_KnowledgeArticleId_UserId]
        ON [dbo].[ArticleLikes] ([KnowledgeArticleId], [UserId]);
END
GO

IF OBJECT_ID(N'[dbo].[ArticleSaves]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ArticleSaves] (
        [ArticleSaveId] BIGINT IDENTITY(1,1) NOT NULL,
        [KnowledgeArticleId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_ArticleSaves] PRIMARY KEY ([ArticleSaveId]),
        CONSTRAINT [FK_ArticleSaves_KnowledgeArticles_KnowledgeArticleId]
            FOREIGN KEY ([KnowledgeArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([KnowledgeArticleId]),
        CONSTRAINT [FK_ArticleSaves_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId])
    );

    CREATE UNIQUE INDEX [IX_ArticleSaves_KnowledgeArticleId_UserId]
        ON [dbo].[ArticleSaves] ([KnowledgeArticleId], [UserId]);
END
GO

IF OBJECT_ID(N'[dbo].[ArticleComments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ArticleComments] (
        [ArticleCommentId] BIGINT IDENTITY(1,1) NOT NULL,
        [KnowledgeArticleId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [Body] NVARCHAR(2000) NOT NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ArticleComments_IsDeleted] DEFAULT (0),
        [DeletedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_ArticleComments] PRIMARY KEY ([ArticleCommentId]),
        CONSTRAINT [FK_ArticleComments_KnowledgeArticles_KnowledgeArticleId]
            FOREIGN KEY ([KnowledgeArticleId]) REFERENCES [dbo].[KnowledgeArticles] ([KnowledgeArticleId]),
        CONSTRAINT [FK_ArticleComments_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId])
    );

    CREATE INDEX [IX_ArticleComments_KnowledgeArticleId]
        ON [dbo].[ArticleComments] ([KnowledgeArticleId]);
    CREATE INDEX [IX_ArticleComments_CreatedAt]
        ON [dbo].[ArticleComments] ([CreatedAt]);
END
GO
