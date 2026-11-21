-- Unlimited credits flag on users + plan sale fields
IF COL_LENGTH(N'dbo.Users', N'HasUnlimitedCredits') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [HasUnlimitedCredits] BIT NOT NULL
        CONSTRAINT [DF_Users_HasUnlimitedCredits] DEFAULT (0);
END
GO

IF COL_LENGTH(N'dbo.Plans', N'SaleEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Plans]
    ADD [SaleEnabled] BIT NOT NULL
        CONSTRAINT [DF_Plans_SaleEnabled] DEFAULT (0);
END
GO

IF COL_LENGTH(N'dbo.Plans', N'SaleDiscountPercent') IS NULL
BEGIN
    ALTER TABLE [dbo].[Plans]
    ADD [SaleDiscountPercent] DECIMAL(5,2) NOT NULL
        CONSTRAINT [DF_Plans_SaleDiscountPercent] DEFAULT (0);
END
GO
