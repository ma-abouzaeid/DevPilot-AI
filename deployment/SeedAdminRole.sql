-- Seed Admin role and promote a user by email.
-- 1) Run this script on the MonsterASP SQL database.
-- 2) Replace the email below with your account.
-- 3) Log out and log in again so the JWT picks up the Admin role.

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = N'Admin')
BEGIN
    INSERT INTO dbo.Roles (Name) VALUES (N'Admin');
END
GO

DECLARE @AdminRoleId INT = (SELECT TOP 1 RoleId FROM dbo.Roles WHERE Name = N'Admin');
DECLARE @UserEmail NVARCHAR(255) = N'tociv47200@backyr.com'; -- <<< change this
DECLARE @UserId INT = (
    SELECT TOP 1 UserId
    FROM dbo.Users
    WHERE Email = @UserEmail AND IsDeleted = 0
);

IF @UserId IS NULL
BEGIN
    RAISERROR(N'User email not found. Update @UserEmail in SeedAdminRole.sql.', 16, 1);
    RETURN;
END

IF NOT EXISTS (
    SELECT 1 FROM dbo.UserRoles WHERE UserId = @UserId AND RoleId = @AdminRoleId
)
BEGIN
    INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @AdminRoleId);
END
GO
