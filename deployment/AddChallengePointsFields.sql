-- Run on MonsterASP / production DB if EF migrations are not applied automatically.
IF COL_LENGTH('dbo.Challenges', 'MaxPoints') IS NULL
    ALTER TABLE dbo.Challenges ADD MaxPoints INT NOT NULL CONSTRAINT DF_Challenges_MaxPoints DEFAULT(100);

IF COL_LENGTH('dbo.Challenges', 'IsPublished') IS NULL
    ALTER TABLE dbo.Challenges ADD IsPublished BIT NOT NULL CONSTRAINT DF_Challenges_IsPublished DEFAULT(1);

IF COL_LENGTH('dbo.ChallengeAttempts', 'SubmittedCode') IS NULL
    ALTER TABLE dbo.ChallengeAttempts ADD SubmittedCode NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.ChallengeEvaluations', 'PointsAwarded') IS NULL
    ALTER TABLE dbo.ChallengeEvaluations ADD PointsAwarded INT NOT NULL CONSTRAINT DF_ChallengeEvaluations_PointsAwarded DEFAULT(0);

IF COL_LENGTH('dbo.ChallengeEvaluations', 'IsPassed') IS NULL
    ALTER TABLE dbo.ChallengeEvaluations ADD IsPassed BIT NOT NULL CONSTRAINT DF_ChallengeEvaluations_IsPassed DEFAULT(0);

IF COL_LENGTH('dbo.ChallengeEvaluations', 'Advice') IS NULL
    ALTER TABLE dbo.ChallengeEvaluations ADD Advice NVARCHAR(MAX) NOT NULL CONSTRAINT DF_ChallengeEvaluations_Advice DEFAULT('');
