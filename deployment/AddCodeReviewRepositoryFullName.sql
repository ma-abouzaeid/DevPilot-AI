-- Keep repo name on saved reviews after GitHub disconnect.
IF COL_LENGTH('dbo.CodeReviews', 'RepositoryFullName') IS NULL
BEGIN
    ALTER TABLE dbo.CodeReviews
    ADD RepositoryFullName nvarchar(300) NOT NULL
        CONSTRAINT DF_CodeReviews_RepositoryFullName DEFAULT (N'');
END
GO

-- Backfill from linked repos still in the DB.
UPDATE cr
SET cr.RepositoryFullName = r.FullName
FROM dbo.CodeReviews cr
INNER JOIN dbo.GitHubRepositories r ON r.GitHubRepositoryId = cr.GitHubRepositoryId
WHERE (cr.RepositoryFullName IS NULL OR LTRIM(RTRIM(cr.RepositoryFullName)) = N'')
  AND r.FullName IS NOT NULL
  AND LTRIM(RTRIM(r.FullName)) <> N'';
GO
