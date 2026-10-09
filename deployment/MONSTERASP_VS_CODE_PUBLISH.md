# DevPilot AI — MonsterASP Deployment & Publishing Guide (VS Code + CLI)

This step-by-step guide explains how to prepare, publish, configure, and troubleshoot the DevPilot AI backend on MonsterASP using VS Code, the .NET CLI, and MonsterASP File Manager or FTP/SFTP.

---

## 1. Prerequisites & Required Tools

- **Local .NET SDK**: .NET 9.0 SDK (`net9.0`)
  - Verify installed version in VS Code terminal:
    ```powershell
    dotnet --version
    ```
- **Editor**: Visual Studio Code
- **MonsterASP Account**:
  - Active website slot (supporting .NET 9 / ASP.NET Core)
  - Active MS SQL Server database instance
  - Access to MonsterASP Control Panel (File Manager, Environment Variables / Application Settings)

---

## 2. Solution & Project Paths

- **Root Repository Path**: `d:\Mahmoud Hema\DEPI\DevPilot AI` (or your local repository root)
- **API Project Path**: `src/DevPilotAI.API/DevPilotAI.API.csproj`
- **Infrastructure Project**: `src/DevPilotAI.Infrastructure/DevPilotAI.Infrastructure.csproj`
- **Output Publish Directory**: `deployment/publish/`
- **SQL Migration Script**: `deployment/DevPilotAI_MonsterASP_Database.sql`

---

## 3. Database Deployment (Run Once Before App Upload)

Before starting the backend application on MonsterASP, create the database schema using the generated SQL script.

1. In MonsterASP Control Panel, navigate to **Databases** > **MS SQL**.
2. Note your database host name, database name, username, and password.
3. Open MonsterASP Web-based SQL Query tool (or connect from your local SSMS / Azure Data Studio using your MonsterASP credentials).
4. Open and execute the script:
   [`deployment/DevPilotAI_MonsterASP_Database.sql`](file:///d:/Mahmoud%20Hema/DEPI/DevPilot%20AI/deployment/DevPilotAI_MonsterASP_Database.sql)
5. The script is idempotent and handles:
   - Creation of `__EFMigrationsHistory`
   - All 4 migrations in order (`FullDB`, `AddSoftDeleteEntities`, `AddSecurityAuditLogs`, `AddPlanAndUsageEntities`)
   - Tables, PKs, non-cascading FKs (`NO ACTION`), indexes, and default seed data (Free Plan & core features).

---

## 4. Build and Publish Commands (VS Code Terminal)

Open a terminal in the root repository folder in VS Code:

### Step 4.1: Clean and Verify Build
```powershell
dotnet clean
dotnet restore
dotnet build --no-incremental -c Release
```
Ensure build outputs **0 Error(s)** and **0 Warning(s)**.

### Step 4.2: Execute Release Publish
```powershell
dotnet publish src/DevPilotAI.API/DevPilotAI.API.csproj -c Release -o deployment/publish
```

---

## 5. Verify the Publish Folder

Inspect `deployment/publish/` and confirm the following components exist:

1. **Core Assembly**: `DevPilotAI.API.dll`
2. **Library Dependencies**: `DevPilotAI.Application.dll`, `DevPilotAI.Domain.dll`, `DevPilotAI.Infrastructure.dll`, `Microsoft.EntityFrameworkCore.SqlServer.dll`, etc.
3. **Runtime Configuration**: `DevPilotAI.API.runtimeconfig.json`
4. **Dependency Manifest**: `DevPilotAI.API.deps.json`
5. **Configuration Files**: `appsettings.json`, `appsettings.Production.json`
6. **IIS Configuration**: `web.config`
7. **Email Templates**: `Email/Templates/PasswordResetEmail.html` and `Email/Templates/VerificationEmail.html`

### Step 5.1: Confirm `web.config`
Open `deployment/publish/web.config` and ensure it contains:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\DevPilotAI.API.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

---

## 6. Prepare Files for MonsterASP Upload

1. Navigate to `deployment/publish/`.
2. Select all files and folders inside `deployment/publish/` (do not zip the outer `publish` folder itself, zip its **contents**).
3. Create a ZIP archive named `publish.zip` (e.g. using right-click > Compress to ZIP, or `Compress-Archive -Path deployment/publish/* -DestinationPath deployment/publish.zip`).

---

## 7. Uploading to MonsterASP

1. Log into your **MonsterASP Control Panel**.
2. Click **Web Sites** > select your site.
3. Open **File Manager** (or connect via FTP / SFTP using your MonsterASP credentials).
4. Navigate to the root web folder:
   - Typically `wwwroot` (or `/site/wwwroot`).
5. If there are default placeholder files (e.g. `index.html` or `hostingstart.html`), remove them or back them up.
6. Upload `publish.zip` into `wwwroot`.
7. Extract `publish.zip` directly into `wwwroot` so that `DevPilotAI.API.dll` and `web.config` sit directly at the root of `wwwroot`.
8. Delete `publish.zip` after extraction.

---

## 8. Configuring Production Settings & Connection String

In the MonsterASP Control Panel, you have two options to configure production settings:

### Option A (Recommended): MonsterASP Environment Variables / App Settings
In the Control Panel under **Configuration** / **Environment Variables**:
Add the following variables (see [`MONSTERASP_ENVIRONMENT_VARIABLES.md`](file:///d:/Mahmoud%20Hema/DEPI/DevPilot%20AI/deployment/MONSTERASP_ENVIRONMENT_VARIABLES.md) for full descriptions):

- `ASPNETCORE_ENVIRONMENT` = `Production`
- `ConnectionStrings__DefaultConnection` = `<YOUR_MONSTERASP_CONNECTION_STRING>`
- `JwtSettings__Secret` = `<YOUR_32_CHAR_JWT_SECRET>`
- `Frontend__BaseUrl` = `https://devpilot-ai-three.vercel.app`
- `AllowedOrigins__0` = `https://devpilot-ai-three.vercel.app`
- `Brevo__ApiKey` = `<YOUR_BREVO_API_KEY>`
- `Brevo__SenderEmail` = `<YOUR_BREVO_VERIFIED_SENDER_EMAIL>`
- `Turnstile__Enabled` = `false` (or `true` if Turnstile widget is active)
- `Turnstile__SecretKey` = `<YOUR_TURNSTILE_SECRET>` (if enabled)

### Option B: Modifying `appsettings.Production.json` directly in File Manager
Alternatively, you can edit `appsettings.Production.json` directly inside MonsterASP File Manager and populate:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<YOUR_MONSTERASP_CONNECTION_STRING>"
  },
  "JwtSettings": {
    "Secret": "<YOUR_32_CHAR_JWT_SECRET>"
  },
  "Brevo": {
    "ApiKey": "<YOUR_BREVO_API_KEY>",
    "SenderEmail": "<YOUR_BREVO_SENDER_EMAIL>"
  }
}
```

---

## 9. Restarting the Application

In the MonsterASP Control Panel:
1. Click **Web Sites** > select your site.
2. Click **Restart** (or **Stop** then **Start**).
3. This recycles the IIS Application Pool and forces ASP.NET Core to reload with the new binaries and configuration.

---

## 10. Verifying the Deployment

### Health Check (Root Endpoint)
In your browser or curl, navigate to your MonsterASP site root:
```text
GET https://<your-site>.monsterasp.net/
```
Expected Response (HTTP 200 OK):
```json
{
  "status": "Online",
  "service": "DevPilot AI API",
  "version": "v1"
}
```

### Authentication Test
Send a registration or login request from your frontend or API client:
```text
POST https://<your-site>.monsterasp.net/api/auth/login
```

---

## 11. Troubleshooting Guide

### 1. HTTP 500.30 — In-Process Startup Failure
- **Cause**: Application crashed during initialization in `Program.cs` (e.g. missing connection string, bad JSON syntax, or missing library).
- **Resolution**:
  1. Open `web.config` in MonsterASP File Manager.
  2. Change `stdoutLogEnabled="false"` to `stdoutLogEnabled="true"`.
  3. Ensure a `logs` directory exists in `wwwroot` (create folder `logs` if absent).
  4. Reproduce the error by requesting the site once.
  5. Check `logs/stdout_*.log` for the exact C# stack trace.
  6. **Important**: Change `stdoutLogEnabled` back to `false` after diagnosis.

### 2. Database Connection Errors / Timeout
- **Symptoms**: Login/Register returns HTTP 500; logs show `SqlException: Cannot open database`.
- **Resolution**:
  - Verify MonsterASP connection string format. Ensure `TrustServerCertificate=True` or `Encrypt=True` aligns with MonsterASP requirements.
  - Verify that MonsterASP SQL Server user has `db_owner` or `db_datareader` + `db_datawriter` permissions on the database.
  - Verify that the schema script (`DevPilotAI_MonsterASP_Database.sql`) was executed before starting the app.

### 3. CORS Errors in Frontend Browser
- **Symptoms**: Browser console shows `CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource`.
- **Resolution**:
  - Verify `AllowedOrigins__0` or `AllowedOrigins` in MonsterASP configuration matches the exact frontend origin: `https://devpilot-ai-three.vercel.app` (do not include a trailing slash).
  - Verify preflight `OPTIONS` requests are not blocked by custom IIS request filtering rules.

### 4. HTTPS / SSL Redirect Loops
- **Symptoms**: Browser reports `ERR_TOO_MANY_REDIRECTS`.
- **Resolution**:
  - The API pipeline is configured with `app.UseForwardedHeaders()`. Ensure MonsterASP SSL is enabled on the domain. If MonsterASP offloads SSL at the proxy level without forwarding HTTPS headers, check MonsterASP SSL certificate settings.

---

## 12. Rollback Procedure

If a deployed release requires immediate rollback:
1. Keep a local copy of your previous publish folder or ZIP archive.
2. In MonsterASP File Manager, delete existing files in `wwwroot`.
3. Upload and extract the previous working ZIP package.
4. Restart the web site in the MonsterASP Control Panel.
