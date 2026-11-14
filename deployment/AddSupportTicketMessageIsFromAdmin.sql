IF COL_LENGTH(N'dbo.SupportTicketMessages', N'IsFromAdmin') IS NULL
BEGIN
    ALTER TABLE [dbo].[SupportTicketMessages]
        ADD [IsFromAdmin] BIT NOT NULL
            CONSTRAINT [DF_SupportTicketMessages_IsFromAdmin] DEFAULT (0);
END
GO

-- Best-effort backfill for messages sent by someone other than the ticket creator.
UPDATE m
SET m.[IsFromAdmin] = 1
FROM [dbo].[SupportTicketMessages] m
INNER JOIN [dbo].[SupportTickets] t ON t.[SupportTicketId] = m.[SupportTicketId]
WHERE m.[SenderUserId] <> t.[CreatedByUserId]
  AND m.[IsFromAdmin] = 0;
GO
