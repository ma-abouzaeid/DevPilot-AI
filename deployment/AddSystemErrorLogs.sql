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
    CREATE INDEX [IX_SystemErrorLogs_StatusCode] ON [dbo].[SystemErrorLogs] ([StatusCode]);
    CREATE INDEX [IX_SystemErrorLogs_ErrorCode] ON [dbo].[SystemErrorLogs] ([ErrorCode]);
    CREATE INDEX [IX_SystemErrorLogs_TraceId] ON [dbo].[SystemErrorLogs] ([TraceId]);
    CREATE INDEX [IX_SystemErrorLogs_UserId] ON [dbo].[SystemErrorLogs] ([UserId]);
END
GO
