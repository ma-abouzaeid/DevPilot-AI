IF OBJECT_ID(N'[dbo].[SupportTickets]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupportTickets] (
        [SupportTicketId] INT IDENTITY(1,1) NOT NULL,
        [TicketCode] NVARCHAR(40) NOT NULL,
        [CreatedByUserId] INT NOT NULL,
        [AssignedAdminUserId] INT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(4000) NOT NULL,
        [Status] TINYINT NOT NULL CONSTRAINT [DF_SupportTickets_Status] DEFAULT (0),
        [ResolutionSummary] NVARCHAR(4000) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [AcceptedAt] DATETIME2 NULL,
        [ResolvedAt] DATETIME2 NULL,
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_SupportTickets] PRIMARY KEY ([SupportTicketId]),
        CONSTRAINT [FK_SupportTickets_Users_CreatedByUserId]
            FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [FK_SupportTickets_Users_AssignedAdminUserId]
            FOREIGN KEY ([AssignedAdminUserId]) REFERENCES [dbo].[Users] ([UserId])
    );

    CREATE UNIQUE INDEX [IX_SupportTickets_TicketCode] ON [dbo].[SupportTickets] ([TicketCode]);
    CREATE INDEX [IX_SupportTickets_Status] ON [dbo].[SupportTickets] ([Status]);
    CREATE INDEX [IX_SupportTickets_CreatedAt] ON [dbo].[SupportTickets] ([CreatedAt]);
    CREATE INDEX [IX_SupportTickets_CreatedByUserId] ON [dbo].[SupportTickets] ([CreatedByUserId]);
    CREATE INDEX [IX_SupportTickets_AssignedAdminUserId] ON [dbo].[SupportTickets] ([AssignedAdminUserId]);
END
GO

IF OBJECT_ID(N'[dbo].[SupportTicketMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupportTicketMessages] (
        [SupportTicketMessageId] BIGINT IDENTITY(1,1) NOT NULL,
        [SupportTicketId] INT NOT NULL,
        [SenderUserId] INT NOT NULL,
        [IsFromAdmin] BIT NOT NULL CONSTRAINT [DF_SupportTicketMessages_IsFromAdmin] DEFAULT (0),
        [Body] NVARCHAR(4000) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_SupportTicketMessages] PRIMARY KEY ([SupportTicketMessageId]),
        CONSTRAINT [FK_SupportTicketMessages_SupportTickets_SupportTicketId]
            FOREIGN KEY ([SupportTicketId]) REFERENCES [dbo].[SupportTickets] ([SupportTicketId]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupportTicketMessages_Users_SenderUserId]
            FOREIGN KEY ([SenderUserId]) REFERENCES [dbo].[Users] ([UserId])
    );

    CREATE INDEX [IX_SupportTicketMessages_SupportTicketId] ON [dbo].[SupportTicketMessages] ([SupportTicketId]);
    CREATE INDEX [IX_SupportTicketMessages_CreatedAt] ON [dbo].[SupportTicketMessages] ([CreatedAt]);
END
GO

IF OBJECT_ID(N'[dbo].[AdminNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AdminNotifications] (
        [AdminNotificationId] BIGINT IDENTITY(1,1) NOT NULL,
        [AdminUserId] INT NOT NULL,
        [SupportTicketId] INT NULL,
        [Type] NVARCHAR(80) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Body] NVARCHAR(1000) NOT NULL,
        [IsRead] BIT NOT NULL CONSTRAINT [DF_AdminNotifications_IsRead] DEFAULT (0),
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_AdminNotifications] PRIMARY KEY ([AdminNotificationId]),
        CONSTRAINT [FK_AdminNotifications_Users_AdminUserId]
            FOREIGN KEY ([AdminUserId]) REFERENCES [dbo].[Users] ([UserId]),
        CONSTRAINT [FK_AdminNotifications_SupportTickets_SupportTicketId]
            FOREIGN KEY ([SupportTicketId]) REFERENCES [dbo].[SupportTickets] ([SupportTicketId])
    );

    CREATE INDEX [IX_AdminNotifications_AdminUserId] ON [dbo].[AdminNotifications] ([AdminUserId]);
    CREATE INDEX [IX_AdminNotifications_CreatedAt] ON [dbo].[AdminNotifications] ([CreatedAt]);
    CREATE INDEX [IX_AdminNotifications_AdminUserId_IsRead] ON [dbo].[AdminNotifications] ([AdminUserId], [IsRead]);
END
GO
