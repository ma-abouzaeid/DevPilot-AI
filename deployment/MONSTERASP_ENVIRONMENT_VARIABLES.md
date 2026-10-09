# DevPilot AI — MonsterASP Production Environment Variables Guide

This document lists the exact configuration keys required and supported by the DevPilot AI backend when deploying to MonsterASP.

In ASP.NET Core on IIS/Windows hosting (including MonsterASP), hierarchical configuration keys can be supplied via Environment Variables by replacing `:` with double underscores (`__`).

---

## 1. Core Runtime Configuration

### `ASPNETCORE_ENVIRONMENT`
- **Required**: YES
- **Secret**: NO
- **Purpose**: Sets the ASP.NET Core hosting environment to Production mode, disabling developer exception pages and enforcing production behaviors.
- **Example Value**: `Production`

---

## 2. Database Connection

### `ConnectionStrings__DefaultConnection`
- **Required**: YES
- **Secret**: YES
- **Purpose**: SQL Server connection string pointing to the MonsterASP hosted database instance.
- **Example Value**: `Server=tcp:mssql.monsterasp.net,1433;Database=db_devpilot;User Id=db_user;Password=<STRONG_DB_PASSWORD>;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;`

---

## 3. JWT Authentication

### `JwtSettings__Secret`
- **Required**: YES
- **Secret**: YES
- **Purpose**: Symmetric HMAC-SHA256 secret key used to sign and validate JWT access tokens. Must be a cryptographically random string of at least 32 characters (256 bits).
- **Example Value**: `<RANDOM_CRYPTOGRAPHIC_STRING_MIN_32_CHARACTERS>`

### `JwtSettings__Issuer`
- **Required**: NO (defaults to `DevPilotAI`)
- **Secret**: NO
- **Purpose**: JWT issuer claim (`iss`) validation.
- **Example Value**: `DevPilotAI`

### `JwtSettings__Audience`
- **Required**: NO (defaults to `DevPilotAIUsers`)
- **Secret**: NO
- **Purpose**: JWT audience claim (`aud`) validation.
- **Example Value**: `DevPilotAIUsers`

### `JwtSettings__ExpiryMinutes`
- **Required**: NO (defaults to `120`)
- **Secret**: NO
- **Purpose**: Access token validity duration in minutes before requiring a refresh token rotation.
- **Example Value**: `120`

---

## 4. CORS & Frontend Integration

### `Frontend__BaseUrl`
- **Required**: YES
- **Secret**: NO
- **Purpose**: Public frontend web application URL used by the backend to formulate password reset and account verification links sent via transactional emails.
- **Example Value**: `https://devpilot-ai-three.vercel.app`

### `AllowedOrigins__0` (or `AllowedOrigins`)
- **Required**: YES
- **Secret**: NO
- **Purpose**: Allowed origin for Cross-Origin Resource Sharing (CORS). Must match the frontend domain without trailing slash. Can be supplied as array elements (`AllowedOrigins__0`) or comma-delimited string (`AllowedOrigins`).
- **Example Value**: `https://devpilot-ai-three.vercel.app`

---

## 5. Brevo Transactional Email Service

### `Brevo__ApiKey`
- **Required**: YES (for verification & password reset emails)
- **Secret**: YES
- **Purpose**: API key for communicating with the Brevo (Sendinblue) Transactional Email REST API (`/v3/smtp/email`).
- **Example Value**: `<MY_BREVO_API_KEY>`

### `Brevo__SenderEmail`
- **Required**: YES
- **Secret**: NO
- **Purpose**: The sender email address verified and authorized in your Brevo account dashboard.
- **Example Value**: `no-reply@devpilot.ai`

### `Brevo__SenderName`
- **Required**: NO (defaults to `DevPilot AI`)
- **Secret**: NO
- **Purpose**: Display name presented to users in transactional email headers.
- **Example Value**: `DevPilot AI`

### `Brevo__BaseUrl`
- **Required**: NO (defaults to `https://api.brevo.com`)
- **Secret**: NO
- **Purpose**: Brevo REST API endpoint.
- **Example Value**: `https://api.brevo.com`

---

## 6. Security & Anti-Bot Protection

### `Turnstile__Enabled`
- **Required**: NO (defaults to `false`)
- **Secret**: NO
- **Purpose**: Controls whether Cloudflare Turnstile CAPTCHA verification is required on the user registration endpoint (`POST /api/auth/register`).
- **Example Value**: `false` (set to `true` once Cloudflare Turnstile keys are generated)

### `Turnstile__SecretKey`
- **Required**: CONDITIONAL (Required when `Turnstile__Enabled=true`)
- **Secret**: YES
- **Purpose**: Cloudflare Turnstile server-side secret key for server-to-server token verification against `challenges.cloudflare.com`.
- **Example Value**: `<MY_CLOUDFLARE_TURNSTILE_SECRET_KEY>`

---

## 7. Diagnostics & Documentation

### `Swagger__Enabled`
- **Required**: NO (defaults to `false` in Production)
- **Secret**: NO
- **Purpose**: Explicitly enables or disables Swagger OpenAPI UI in Production mode (`/swagger`).
- **Example Value**: `false`

---

## 8. Optional AI Gateway Keys (If Activated)

### `AIGateway__GeminiApiKey`
- **Required**: OPTIONAL
- **Secret**: YES
- **Purpose**: Google Gemini API key if AI gateway features are used.
- **Example Value**: `<MY_GEMINI_API_KEY>`

### `AIGateway__GroqApiKey`
- **Required**: OPTIONAL
- **Secret**: YES
- **Purpose**: Groq Cloud API key if AI gateway features are used.
- **Example Value**: `<MY_GROQ_API_KEY>`

### `AIGateway__OpenRouterApiKey`
- **Required**: OPTIONAL
- **Secret**: YES
- **Purpose**: OpenRouter API key if AI gateway features are used.
- **Example Value**: `<MY_OPENROUTER_API_KEY>`

### `AIGateway__GitHubModelsApiKey`
- **Required**: OPTIONAL
- **Secret**: YES
- **Purpose**: GitHub Models Personal Access Token / API key if AI gateway features are used.
- **Example Value**: `<MY_GITHUB_PAT>`
