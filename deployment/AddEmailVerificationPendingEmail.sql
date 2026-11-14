IF COL_LENGTH('dbo.EmailVerifications', 'PendingEmail') IS NULL
    ALTER TABLE dbo.EmailVerifications ADD PendingEmail NVARCHAR(255) NULL;
