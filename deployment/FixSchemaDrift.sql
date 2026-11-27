-- Apply missing columns that cause Reset Password / Feedback DB persist errors on MonsterASP.
-- Safe to re-run.

IF COL_LENGTH('dbo.EmailVerifications', 'PendingEmail') IS NULL
BEGIN
    ALTER TABLE dbo.EmailVerifications ADD PendingEmail NVARCHAR(255) NULL;
END
GO

IF COL_LENGTH('dbo.UserFeedbacks', 'Rating') IS NULL
BEGIN
    ALTER TABLE dbo.UserFeedbacks ADD Rating TINYINT NULL;
END
GO
