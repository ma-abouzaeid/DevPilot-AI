-- Telegram Smart Support + feedback + ticket attachments
IF OBJECT_ID(N'[dbo].[TelegramConversations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TelegramConversations] (
        [TelegramConversationId] BIGINT IDENTITY(1,1) NOT NULL,
        [ChatId] BIGINT NOT NULL,
        [LinkedUserId] INT NULL,
        [State] NVARCHAR(80) NOT NULL,
        [PayloadJson] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_TelegramConversations] PRIMARY KEY ([TelegramConversationId]),
        CONSTRAINT [FK_TelegramConversations_Users_LinkedUserId]
            FOREIGN KEY ([LinkedUserId]) REFERENCES [dbo].[Users] ([UserId])
    );
    CREATE UNIQUE INDEX [IX_TelegramConversations_ChatId] ON [dbo].[TelegramConversations] ([ChatId]);
END
GO

IF OBJECT_ID(N'[dbo].[UserFeedbacks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserFeedbacks] (
        [UserFeedbackId] BIGINT IDENTITY(1,1) NOT NULL,
        [UserId] INT NULL,
        [TelegramChatId] BIGINT NULL,
        [Source] NVARCHAR(40) NOT NULL,
        [Category] NVARCHAR(80) NOT NULL,
        [Message] NVARCHAR(4000) NOT NULL,
        [Status] NVARCHAR(40) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [ReviewedAt] DATETIME2 NULL,
        [ReviewedByUserId] INT NULL,
        CONSTRAINT [PK_UserFeedbacks] PRIMARY KEY ([UserFeedbackId]),
        CONSTRAINT [FK_UserFeedbacks_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [FK_UserFeedbacks_Users_ReviewedByUserId]
            FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[Users] ([UserId])
    );
    CREATE INDEX [IX_UserFeedbacks_CreatedAt] ON [dbo].[UserFeedbacks] ([CreatedAt]);
    CREATE INDEX [IX_UserFeedbacks_Status] ON [dbo].[UserFeedbacks] ([Status]);
END
GO

IF COL_LENGTH(N'dbo.SupportTicketMessages', N'AttachmentUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[SupportTicketMessages]
    ADD [AttachmentUrl] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.SupportTicketMessages', N'AttachmentContentType') IS NULL
BEGIN
    ALTER TABLE [dbo].[SupportTicketMessages]
    ADD [AttachmentContentType] NVARCHAR(100) NULL;
END
GO
