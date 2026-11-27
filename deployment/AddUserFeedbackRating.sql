-- Optional 1–5 star rating on UserFeedbacks (web feedback form).
IF COL_LENGTH('dbo.UserFeedbacks', 'Rating') IS NULL
BEGIN
    ALTER TABLE dbo.UserFeedbacks
    ADD Rating TINYINT NULL;
END
GO
