-- Credit costs per action (editable from Admin).
-- Safe to re-run.

IF OBJECT_ID(N'dbo.CreditActionCosts', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CreditActionCosts] (
        [ActionKey] NVARCHAR(50) NOT NULL,
        [DisplayName] NVARCHAR(100) NOT NULL,
        [Credits] INT NOT NULL,
        [ShowOnPlansPage] BIT NOT NULL CONSTRAINT [DF_CreditActionCosts_ShowOnPlansPage] DEFAULT (1),
        [SortOrder] INT NOT NULL CONSTRAINT [DF_CreditActionCosts_SortOrder] DEFAULT (0),
        [UpdatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_CreditActionCosts] PRIMARY KEY ([ActionKey])
    );

    CREATE INDEX [IX_CreditActionCosts_SortOrder] ON [dbo].[CreditActionCosts] ([SortOrder]);
END
GO

MERGE [dbo].[CreditActionCosts] AS target
USING (VALUES
    (N'Debugging', N'AI Debug', 5, 1, 1),
    (N'KnowledgeAdaptation', N'Knowledge reuse', 2, 1, 2),
    (N'ChallengeEvaluation', N'Challenge evaluate', 5, 1, 3),
    (N'CodeReview', N'Code Review', 15, 1, 4),
    (N'ChallengeGeneration', N'Challenge generation', 3, 0, 5)
) AS source ([ActionKey], [DisplayName], [Credits], [ShowOnPlansPage], [SortOrder])
ON target.[ActionKey] = source.[ActionKey]
WHEN NOT MATCHED THEN
    INSERT ([ActionKey], [DisplayName], [Credits], [ShowOnPlansPage], [SortOrder], [UpdatedAt])
    VALUES (source.[ActionKey], source.[DisplayName], source.[Credits], source.[ShowOnPlansPage], source.[SortOrder], SYSUTCDATETIME());
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20261114154347_AddCreditActionCosts'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114154347_AddCreditActionCosts', N'9.0.2');
END
GO
