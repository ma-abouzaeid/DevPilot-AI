IF COL_LENGTH(N'dbo.SupportTicketMessages', N'IsFromAdmin') IS NULL
BEGIN
    ALTER TABLE [dbo].[SupportTicketMessages]
        ADD [IsFromAdmin] BIT NOT NULL
            CONSTRAINT [DF_SupportTicketMessages_IsFromAdmin] DEFAULT (0);
END
GO

-- Mark older messages from non-creators as admin.
UPDATE m
SET m.[IsFromAdmin] = 1
FROM [dbo].[SupportTicketMessages] m
INNER JOIN [dbo].[SupportTickets] t ON t.[SupportTicketId] = m.[SupportTicketId]
WHERE m.[SenderUserId] <> t.[CreatedByUserId]
  AND m.[IsFromAdmin] = 0;
GO
