-- Seed Pro plan + github_code_review feature (safe to re-run).

IF NOT EXISTS (SELECT 1 FROM dbo.Features WHERE [Key] = N'github_code_review')
BEGIN
    SET IDENTITY_INSERT dbo.Features ON;
    INSERT INTO dbo.Features (FeatureId, [Key], Name, Description, Category, CreatedAt, UpdatedAt)
    VALUES (3, N'github_code_review', N'GitHub Code Review', N'AI code review directly from connected GitHub repositories.', N'AI', SYSUTCDATETIME(), SYSUTCDATETIME());
    SET IDENTITY_INSERT dbo.Features OFF;
END

IF NOT EXISTS (SELECT 1 FROM dbo.Plans WHERE Code = N'PRO')
BEGIN
    SET IDENTITY_INSERT dbo.Plans ON;
    INSERT INTO dbo.Plans (PlanId, Name, Code, Description, Status, IsDefault, IsPublic, PeriodType, CreditsPerPeriod, Price, Currency, CreatedAt, UpdatedAt)
    VALUES (2, N'Pro', N'PRO', N'Higher AI credits plus GitHub Code Review for teams that ship faster.', N'Active', 0, 1, N'Monthly', 1000, 20.00, N'USD', SYSUTCDATETIME(), SYSUTCDATETIME());
    SET IDENTITY_INSERT dbo.Plans OFF;
END
ELSE
    UPDATE dbo.Plans SET Price = 20.00, UpdatedAt = SYSUTCDATETIME() WHERE Code = N'PRO';

-- Free plan: review disabled
IF NOT EXISTS (SELECT 1 FROM dbo.PlanFeatures WHERE PlanId = 1 AND FeatureId = 3)
    INSERT INTO dbo.PlanFeatures (PlanId, FeatureId, IsEnabled, LimitValue, ConfigJson, CreatedAt, UpdatedAt)
    VALUES (1, 3, 0, 0, N'{"enabled": false}', SYSUTCDATETIME(), SYSUTCDATETIME());
ELSE
    UPDATE dbo.PlanFeatures SET IsEnabled = 0, LimitValue = 0, ConfigJson = N'{"enabled": false}', UpdatedAt = SYSUTCDATETIME()
    WHERE PlanId = 1 AND FeatureId = 3;

-- Pro plan features
IF NOT EXISTS (SELECT 1 FROM dbo.PlanFeatures WHERE PlanId = 2 AND FeatureId = 1)
    INSERT INTO dbo.PlanFeatures (PlanId, FeatureId, IsEnabled, LimitValue, ConfigJson, CreatedAt, UpdatedAt)
    VALUES (2, 1, 1, 1000, N'{"enabled": true}', SYSUTCDATETIME(), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.PlanFeatures WHERE PlanId = 2 AND FeatureId = 2)
    INSERT INTO dbo.PlanFeatures (PlanId, FeatureId, IsEnabled, LimitValue, ConfigJson, CreatedAt, UpdatedAt)
    VALUES (2, 2, 1, 1000, N'{"credits": 1000}', SYSUTCDATETIME(), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.PlanFeatures WHERE PlanId = 2 AND FeatureId = 3)
    INSERT INTO dbo.PlanFeatures (PlanId, FeatureId, IsEnabled, LimitValue, ConfigJson, CreatedAt, UpdatedAt)
    VALUES (2, 3, 1, NULL, N'{"enabled": true}', SYSUTCDATETIME(), SYSUTCDATETIME());
ELSE
    UPDATE dbo.PlanFeatures SET IsEnabled = 1, ConfigJson = N'{"enabled": true}', UpdatedAt = SYSUTCDATETIME()
    WHERE PlanId = 2 AND FeatureId = 3;
