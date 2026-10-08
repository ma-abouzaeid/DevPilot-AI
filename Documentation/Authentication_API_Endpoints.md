# DevPilot AI Authentication API Documentation

## 1. Overview

DevPilot AI provides a robust, production-grade authentication and session management subsystem built on ASP.NET Core (.NET 9) following Clean Architecture principles. It delivers secure identity management, account registration with bot prevention, multi-method email verification, credential authentication, HMAC-SHA256 JWT access token issuance, sliding-window cryptographic refresh token rotation with reuse detection, and safe password recovery workflows.

### 1.1 Architecture & Protocols

- **Base URL Structure:** `https://<api-host>/api`
- **Default Local Development URL:** `https://localhost:7001/api` (or `http://localhost:5000/api`)
- **Transport Security:** All endpoints require HTTPS in production. HSTS and standard secure transport headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection: 1; mode=block`, `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy`) are enforced by the API middleware pipeline.
- **Data Interchange Format:** `application/json; charset=utf-8`
- **API Architectural Pattern:** RESTful JSON Web API with standardized JSON envelope payloads.

### 1.2 Authentication & Session Strategy

The authentication model utilizes short-lived JSON Web Tokens (JWT) for stateless API authorization and cryptographically secure, rotating Refresh Tokens for session persistence:

- **Access Token:** Short-lived JWT (configurable lifetime, 60–120 minutes) signed using HMAC-SHA256. Carries user identity (`sub`, `nameidentifier`, `email`, `name`, `jti`) and authorization role claims (`Role`). Sent by clients in the standard HTTP `Authorization` request header.
- **Refresh Token:** Cryptographically random 256-bit entropy token (valid for 7 days). Client receives an opaque string token. The backend **never** stores raw refresh tokens; only a SHA-256 hash is persisted in the database.
- **Token Rotation & Session Protection:** Every refresh token exchange issues a brand-new refresh token and marks the previous token as used (`IsUsed = true`), recording the replacement token hash (`ReplacedByTokenHash`). If a used or compromised token is re-submitted, the system triggers automated reuse detection and immediately revokes all active sessions for that user.
- **Role-Based Authorization:** Supports `Developer` (assigned by default to all registered users) and `Administrator` roles. Role claims are embedded in the access token claims identity.
- **Email Verification Mandate:** Accounts are created with `EmailVerifiedAt = NULL`. Authenticated login is blocked (HTTP 401) until the developer completes email verification via 6-digit OTP code or cryptographic link token.

---

### 1.3 High-Level Authentication Lifecycles

#### Standard Registration & Authentication Flow

```
[ Client ]                            [ DevPilot AI Backend ]               [ Brevo / Mailer ]
    |                                            |                                   |
    |-- 1. POST /api/auth/register ------------->|                                   |
    |      (Name, Email, Password, Turnstile)    |-- Validates input & Turnstile     |
    |                                            |-- Hashes password (PasswordHasher)|
    |                                            |-- Generates 6-digit OTP Hash      |
    |                                            |-- Dispatches verification email ->|
    |<-- Returns 201 Created (RequiresVerif.) ---|                                   |
    |                                            |                                   |
    |-- 2. POST /api/auth/verify-email ---------->|                                   |
    |      (Email, Code OR Token)                |-- Verifies hash & expiration      |
    |                                            |-- Sets EmailVerifiedAt timestamp  |
    |<-- Returns 200 OK (Success: true) ---------|                                   |
    |                                            |                                   |
    |-- 3. POST /api/auth/login ----------------->|                                   |
    |      (Email, Password)                     |-- Verifies password & verification|
    |                                            |-- Creates JWT & Refresh Token Hash|
    |<-- Returns 200 OK (Access + Refresh Token)-|                                   |
    |                                            |                                   |
    |-- 4. Authorized Requests (Bearers) ------->|                                   |
    |      Authorization: Bearer <AccessToken>   |-- Validates signature & claims    |
    |<-- Returns 200 OK / Data ------------------|                                   |
    |                                            |                                   |
    |-- 5. POST /api/auth/refresh -------------->|                                   |
    |      (RefreshToken)                        |-- Validates token hash            |
    |                                            |-- Rotates: marks old used         |
    |                                            |-- Issues new Access + Refresh     |
    |<-- Returns 200 OK (New Tokens) ------------|                                   |
    |                                            |                                   |
    |-- 6. POST /api/auth/logout --------------->|                                   |
    |      (RefreshToken)                        |-- Revokes refresh token           |
    |<-- Returns 200 OK (Logged out) ------------|                                   |
```

#### Password Recovery Flow

```
[ Client ]                            [ DevPilot AI Backend ]               [ Brevo / Mailer ]
    |                                            |                                   |
    |-- 1. POST /api/auth/forgot-password ------>|                                   |
    |      (Email)                               |-- Safe lookup (Enumeration safe)  |
    |                                            |-- Generates Reset Token Hash      |
    |                                            |-- Dispatches reset email -------->|
    |<-- Returns 200 OK (Safe message) ----------|                                   |
    |                                            |                                   |
    |-- 2. POST /api/auth/reset-password ------->|                                   |
    |      (Token, NewPassword)                  |-- Validates token hash            |
    |                                            |-- Hashes new password             |
    |                                            |-- Revokes all active refresh sess.|
    |<-- Returns 200 OK (Success: true) ---------|                                   |
    |                                            |                                   |
    |-- 3. POST /api/auth/login ----------------->|                                   |
    |      (Email, NewPassword)                  |-- Normal login resumes            |
    |<-- Returns 200 OK (Access + Refresh) ------|                                   |
```

---

## 2. Global API Rules & Conventions

### 2.1 Standard Headers

| Header | Type | Required | Description |
| :--- | :--- | :--- | :--- |
| `Content-Type` | String | Yes | Must be `application/json` for all POST requests containing a body. |
| `Accept` | String | Optional | Recommended `application/json`. |
| `Authorization` | String | Protected Only | Format: `Bearer <access-token>`. Required for protected developer or administrator endpoints. |

### 2.2 Access Levels

- **Public:** Accessible without authentication (e.g., Register, Verify Email, Resend Verification, Login, Forgot Password, Reset Password).
- **Refresh Token Authenticated:** Requires a valid, active cryptographic refresh token provided in the JSON payload body (e.g., `/api/auth/refresh`, `/api/auth/logout`).
- **Protected (Bearer Token):** Requires a valid, non-expired JWT Access Token passed in the `Authorization: Bearer <token>` header.

### 2.3 Envelope Formats

#### Successful Response Envelope
All successful controller responses wrap their result in a standard envelope:

```json
{
  "success": true,
  "data": {
    ...
  }
}
```

*Note: For `POST /api/auth/register`, the response status is HTTP `201 Created`.*  
*For all other successful auth endpoints, the response status is HTTP `200 OK`.*

#### Error Response Envelope
All API exceptions and validation failures are intercepted by `ExceptionHandlingMiddleware` and serialized into a uniform, safe response structure without stack traces or sensitive internals:

```json
{
  "success": false,
  "errorCode": "ERROR_CODE",
  "message": "Safe user-facing explanation.",
  "requestId": "0HN1234567890:00000001"
}
```

### 2.4 Error Code & HTTP Status Code Mapping

| HTTP Status | Error Code (`errorCode`) | Exception Type Trigger | Common Scenarios |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `"BAD_REQUEST"` | `ArgumentException` / Model Validation | Missing required fields, invalid email format, weak password, or empty tokens. |
| **`401 Unauthorized`** | `"UNAUTHORIZED"` | `UnauthorizedAccessException` | Bad credentials, unverified email address, expired/revoked refresh token, or detected token reuse. |
| **`404 Not Found`** | `"NOT_FOUND"` | `KeyNotFoundException` | Account not found during verification or resend requests. |
| **`409 Conflict`** | `"CONFLICT"` | `InvalidOperationException` / `DbUpdateException` | Account with email already exists, verification cooldown active, invalid OTP code, max attempts reached, or Turnstile check failure. |
| **`500 Internal Server Error`** | `"INTERNAL_SERVER_ERROR"` | Unhandled `Exception` | Unexpected internal database, provider, or server failure. |

---

## 3. Authentication Endpoint Reference

---

### POST /api/auth/register

#### Purpose
Registers a new developer account, hashes credentials using ASP.NET Core `PasswordHasher<User>`, assigns the default `Developer` role, generates a hashed 6-digit email verification OTP, and initiates account verification.

#### Jira Scope
- **Primary:** `DA-34` (Endpoint routing, user provisioning, role attachment)
- **Supporting:** `DA-33` (DTO contracts, model validation, password hashing, verification records)

#### Access
**Public** (Unauthenticated)

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
```

#### Request Body
```json
{
  "name": "Mahmoud Ahmed",
  "email": "developer@example.com",
  "password": "Password123",
  "turnstileToken": "0.AbCdEf12345..."
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `name` | String | Yes | Length: 2–150 chars | Full name of the developer. |
| `email` | String | Yes | Valid email format, max 255 chars | Unique account email address. Normalized to lowercase. |
| `password` | String | Yes | Min 8, max 100 chars; must contain $\ge 1$ uppercase letter, $\ge 1$ lowercase letter, $\ge 1$ digit | Plaintext developer password. Never logged or stored raw. |
| `turnstileToken` | String | No | Valid Cloudflare Turnstile token | Bot verification token passed from client widget. |

#### Successful Response
**HTTP Status:** `201 Created`

```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Registration successful. A verification code has been sent to your email.",
    "email": "developer@example.com",
    "requiresVerification": true
  }
}
```

##### Response Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `success` | Boolean | Confirmation that the registration completed. |
| `message` | String | User-facing instruction notifying that verification is required. |
| `email` | String | The registered email address. |
| `requiresVerification`| Boolean | Always `true` for newly registered developers. |

#### Error Responses

##### 400 Bad Request — Validation Failure
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST",
  "message": "Password must contain at least one uppercase letter, one lowercase letter, and one number.",
  "requestId": "0HN1234567890:00000001"
}
```

##### 409 Conflict — Duplicate Email
```json
{
  "success": false,
  "errorCode": "CONFLICT",
  "message": "An account with this email address already exists.",
  "requestId": "0HN1234567890:00000002"
}
```

##### 409 Conflict — Bot Check Failed
```json
{
  "success": false,
  "errorCode": "CONFLICT",
  "message": "Security check failed. Please complete the captcha verification.",
  "requestId": "0HN1234567890:00000003"
}
```

#### Business & Security Rules
1. Passwords are never stored in plaintext or generic SHA-256; PBKDF2/HMAC-SHA512 via `PasswordHasher<User>` is used.
2. Email uniqueness is verified at both application and database level (unique index on `Users.Email`).
3. An `EmailVerification` entity is created with a 15-minute expiration (`ExpiresAt = DateTime.UtcNow.AddMinutes(15)`) and `Attempts = 0`.
4. The user is assigned the `"Developer"` role in `UserRoles`.

---

### POST /api/auth/verify-email

#### Purpose
Verifies the developer's email address using either a 6-digit numeric OTP code or a cryptographic URL token. Upon successful verification, marks `Users.EmailVerifiedAt` to enable authenticated login.

#### Jira Scope
- **Primary:** `DA-33` (Email Verification API contracts, OTP verification logic, token attempt limits)

#### Access
**Public** (Unauthenticated)

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
```

#### Request Body

##### Variant A: 6-Digit OTP Verification
```json
{
  "email": "developer@example.com",
  "code": "483921"
}
```

##### Variant B: Verification Link Token
```json
{
  "token": "verification-token-xyz..."
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `email` | String | Conditional | Valid email format | Required when verifying with `code`. |
| `code` | String | Conditional | Max 20 chars, numeric | 6-digit OTP code received in email. |
| `token` | String | Conditional | String | Cryptographic verification token from email link. |

*Note: At least one of `code` or `token` must be provided.*

#### Successful Response
**HTTP Status:** `200 OK`

```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Email verified successfully."
  }
}
```

#### Error Responses

##### 400 Bad Request — Missing Parameters
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST",
  "message": "Either verification code or verification token must be provided.",
  "requestId": "0HN1234567890:00000004"
}
```

##### 404 Not Found — User Account Missing
```json
{
  "success": false,
  "errorCode": "NOT_FOUND",
  "message": "User account not found.",
  "requestId": "0HN1234567890:00000005"
}
```

##### 409 Conflict — Invalid / Expired Code or Max Attempts Exceeded
```json
{
  "success": false,
  "errorCode": "CONFLICT",
  "message": "Invalid or expired verification code.",
  "requestId": "0HN1234567890:00000006"
}
```

##### 409 Conflict — Already Verified
```json
{
  "success": false,
  "errorCode": "CONFLICT",
  "message": "Email address is already verified.",
  "requestId": "0HN1234567890:00000007"
}
```

#### Business & Security Rules
1. OTP codes are compared against the stored SHA-256 hash `CodeHash`.
2. Failed code submissions increment `EmailVerifications.Attempts`.
3. If `Attempts >= 5`, the verification record is locked and subsequent attempts are rejected.
4. Single-use: Once verified, `VerifiedAt` is populated, rendering the record permanently consumed.

---

### POST /api/auth/resend-verification

#### Purpose
Invalidates existing unverified codes and generates a fresh 6-digit verification OTP. Enforces a 60-second cooldown window to protect against mail server flooding and brute force attempts.

#### Jira Scope
- **Primary:** `DA-33` (Resend verification logic, rate limiting cooldown)

#### Access
**Public** (Unauthenticated)

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
```

#### Request Body
```json
{
  "email": "developer@example.com"
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `email` | String | Yes | Valid email format | Developer account email address. |

#### Successful Response
**HTTP Status:** `200 OK`

```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Verification code has been resent to your email."
  }
}
```

#### Error Responses

##### 404 Not Found — User Account Missing
```json
{
  "success": false,
  "errorCode": "NOT_FOUND",
  "message": "User account not found.",
  "requestId": "0HN1234567890:00000008"
}
```

##### 409 Conflict — Cooldown Window Active
```json
{
  "success": false,
  "errorCode": "CONFLICT",
  "message": "Please wait before requesting another verification code.",
  "requestId": "0HN1234567890:00000009"
}
```

##### 409 Conflict — Account Already Verified
```json
{
  "success": false,
  "errorCode": "CONFLICT",
  "message": "This account is already verified.",
  "requestId": "0HN1234567890:00000010"
}
```

#### Business & Security Rules
1. Server verifies if an unverified `EmailVerification` record was created within the last 60 seconds (`CreatedAt > DateTime.UtcNow.AddSeconds(-60)`).
2. If the cooldown is active, the request is rejected with HTTP 409 Conflict.

---

### POST /api/auth/login

#### Purpose
Authenticates a verified developer using email and password. On successful authentication, generates a signed HMAC-SHA256 JWT access token (with role claims) and a cryptographically random refresh token.

#### Jira Scope
- **Primary:** `DA-34` (BE-01 Authentication Endpoints, session creation, JWT + refresh token issuance, role claims)

#### Access
**Public** (Unauthenticated)

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
```

#### Request Body
```json
{
  "email": "developer@example.com",
  "password": "Password123"
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `email` | String | Yes | Valid email format | Registered developer email address. |
| `password` | String | Yes | Non-empty string | Developer plaintext password. |

#### Successful Response
**HTTP Status:** `200 OK`

```json
{
  "success": true,
  "data": {
    "success": true,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwibmFtZWlkZW50aWZpZXIiOiIxIiwiZW1haWwiOiJkZXZlbG9wZXJAZXhhbXBsZS5jb20iLCJuYW1lIjoiTWFobW91ZCBBaG1lZCIsImp0aSI6Ijg4YWE0ZDA1LTgyNDktNDVjMC1hMjQxLTYxOGVlZDc4ZDExYSIsInJvbGUiOiJEZXZlbG9wZXIiLCJuYmYiOjE3Mjk4NTk2MDAsImV4cCI6MTcyOTg2NjgwMCwiaXNzIjoiRGV2UGlsb3RBSSIsImF1ZCI6IkRldlBpbG90QUlVc2VycyJ9.sample_signature...",
    "refreshToken": "refresh_1_s8F_2eK3p9Z-rQ87a...",
    "user": {
      "userId": 1,
      "name": "Mahmoud Ahmed",
      "email": "developer@example.com",
      "emailVerifiedAt": "2026-10-08T15:00:00Z"
    }
  }
}
```

##### Response Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `success` | Boolean | Indicates successful login. |
| `accessToken` | String | Signed HMAC-SHA256 JWT string. Used for API Bearer authentication. |
| `refreshToken` | String | Opaque string token (256 bits of cryptographic entropy). Used to rotate sessions. |
| `user.userId` | Integer | Unique identifier of the authenticated user. |
| `user.name` | String | Developer's full name. |
| `user.email` | String | Normalized email address. |
| `user.emailVerifiedAt`| String / Null | ISO 8601 UTC timestamp when the email was verified. |

#### Error Responses

##### 401 Unauthorized — Invalid Credentials
```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED",
  "message": "Invalid email or password.",
  "requestId": "0HN1234567890:00000011"
}
```

##### 401 Unauthorized — Email Not Verified
```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED",
  "message": "Email is not verified. Please verify your account before logging in.",
  "requestId": "0HN1234567890:00000012"
}
```

##### 400 Bad Request — Missing Fields
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST",
  "message": "Email is required.",
  "requestId": "0HN1234567890:00000013"
}
```

#### Business & Security Rules
1. Password verification failure returns identical generic error `"Invalid email or password."` to prevent account enumeration.
2. Login is strictly forbidden if `EmailVerifiedAt == null`.
3. Loads user roles (`User.UserRoles.Role`). Defaults to `"Developer"` if none found.
4. Generates a fresh `RefreshToken` record with `ExpiresAt = DateTime.UtcNow.AddDays(7)`, storing only its SHA-256 hash.

---

### POST /api/auth/refresh

#### Purpose
Refreshes an expired or expiring access token using a valid refresh token. Implements strict single-use refresh token rotation and automated reuse detection to protect against session hijacking.

#### Jira Scope
- **Primary:** `DA-34` (BE-01 Authentication Endpoints, token refresh, rotation, reuse detection)

#### Access
**Requires Refresh Token**

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
```

#### Request Body
```json
{
  "refreshToken": "refresh_1_s8F_2eK3p9Z-rQ87a..."
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `refreshToken` | String | Yes | Non-empty string | The opaque refresh token issued during login or the previous refresh. |

#### Successful Response
**HTTP Status:** `200 OK`

```json
{
  "success": true,
  "data": {
    "success": true,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "refresh_1_b7H_9vX1k0L-mW52c..."
  }
}
```

##### Response Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `success` | Boolean | Indicates successful rotation. |
| `accessToken` | String | Newly generated signed JWT access token. |
| `refreshToken` | String | Newly generated cryptographic refresh token for the next rotation cycle. |

#### Error Responses

##### 401 Unauthorized — Expired / Revoked / Non-Existent Token
```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED",
  "message": "Invalid or expired refresh token.",
  "requestId": "0HN1234567890:00000014"
}
```

##### 401 Unauthorized — Token Reuse Attack Detected
```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED",
  "message": "Refresh token reuse detected. All active sessions have been revoked for security.",
  "requestId": "0HN1234567890:00000015"
}
```

##### 400 Bad Request — Missing Refresh Token
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST",
  "message": "Refresh token is required.",
  "requestId": "0HN1234567890:00000016"
}
```

#### Business & Security Rules
1. **Token Verification:** Looks up the SHA-256 hash of the presented refresh token. Rejects if not found, revoked (`IsRevoked == true`), or expired (`ExpiresAt <= DateTime.UtcNow`).
2. **Reuse Detection:** If `IsUsed == true`, the backend flags a replay or hijacking attempt. It queries **all** active refresh tokens for that user and marks them revoked (`IsRevoked = true`, `RevokedAt = DateTime.UtcNow`), terminating all sessions.
3. **Rotation:** The incoming token is marked `IsUsed = true` and `ReplacedByTokenHash` is updated with the SHA-256 hash of the newly generated token.
4. The client **must** replace its stored refresh token with the newly issued token.

---

### POST /api/auth/logout

#### Purpose
Terminates the current user session by revoking the specified refresh token in the database.

#### Jira Scope
- **Primary:** `DA-34` (BE-01 Authentication Endpoints, session termination)

#### Access
**Requires Refresh Token**

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
Authorization: Bearer <access-token>  (Optional if client session is active)
```

#### Request Body
```json
{
  "refreshToken": "refresh_1_b7H_9vX1k0L-mW52c..."
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `refreshToken` | String | Yes | Non-empty string | The active refresh token to be revoked. |

#### Successful Response
**HTTP Status:** `200 OK`

```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Logged out successfully."
  }
}
```

#### Error Responses

##### 400 Bad Request — Missing Refresh Token
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST",
  "message": "Refresh token is required.",
  "requestId": "0HN1234567890:00000017"
}
```

#### Business & Security Rules
1. The backend hashes the provided token and finds the matching `RefreshToken` record.
2. If found and active, sets `IsRevoked = true` and `RevokedAt = DateTime.UtcNow`.
3. If the token is already revoked or not found, the endpoint returns HTTP 200 OK idempotently to avoid timing or enumeration leaks.
4. Subsequent attempts to call `POST /api/auth/refresh` with the revoked token will be rejected with HTTP 401 Unauthorized.

---

### POST /api/auth/forgot-password

#### Purpose
Initiates the password recovery workflow. If the submitted email corresponds to an existing user, generates a hashed single-use password reset link token valid for 30 minutes. Implements strict account enumeration protection.

#### Jira Scope
- **Primary:** `DA-33` (Password recovery contracts, token hashing, enumeration protection)

#### Access
**Public** (Unauthenticated)

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
```

#### Request Body
```json
{
  "email": "developer@example.com"
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `email` | String | Yes | Valid email format | Email address associated with the account. |

#### Successful Response
**HTTP Status:** `200 OK`

```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "If the email is registered, a password reset link has been sent to your email address."
  }
}
```

#### Error Responses

##### 400 Bad Request — Invalid Email Format
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST",
  "message": "Invalid email format.",
  "requestId": "0HN1234567890:00000018"
}
```

#### Business & Security Rules
1. **Account Enumeration Protection:** The endpoint returns the identical success message regardless of whether the email exists in the database.
2. If the user exists, a reset token is generated with 30-minute expiration (`ExpiresAt = DateTime.UtcNow.AddMinutes(30)`), stored in `EmailVerifications` with `Method = "ResetLink"`.
3. Only the SHA-256 hash of the reset token is persisted.

---

### POST /api/auth/reset-password

#### Purpose
Validates the password reset token, updates the developer's credentials using `PasswordHasher<User>`, marks the reset token as consumed, and revokes all active refresh tokens for the user to invalidate all active sessions.

#### Jira Scope
- **Primary:** `DA-33` (Reset password contracts, token consumption, session revocation)

#### Access
**Public** (Unauthenticated with valid reset token)

#### HTTP Method
`POST`

#### Request Headers
```http
Content-Type: application/json
```

#### Request Body
```json
{
  "token": "reset_1_8fK_9eM2p0Q...",
  "newPassword": "NewStrongPassword123"
}
```

##### Field Specifications
| Property | Type | Required | Constraints | Description |
| :--- | :--- | :--- | :--- | :--- |
| `token` | String | Yes | Non-empty string | Raw reset token received in the password recovery email. |
| `newPassword` | String | Yes | Min 8, max 100 chars; $\ge 1$ uppercase, $\ge 1$ lowercase, $\ge 1$ digit | New plaintext password. |

#### Successful Response
**HTTP Status:** `200 OK`

```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Password reset successfully. You can now log in with your new password."
  }
}
```

#### Error Responses

##### 400 Bad Request — Password Complexity Failure
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST",
  "message": "Password must contain at least one uppercase letter, one lowercase letter, and one number.",
  "requestId": "0HN1234567890:00000019"
}
```

##### 409 Conflict — Invalid / Expired Token
```json
{
  "success": false,
  "errorCode": "CONFLICT",
  "message": "Invalid or expired reset token.",
  "requestId": "0HN1234567890:00000020"
}
```

#### Business & Security Rules
1. Looks up the reset record in `EmailVerifications` matching `TokenHash` where `Method == "ResetLink"`, `VerifiedAt == null`, `ExpiresAt > DateTime.UtcNow`, and `Attempts < 5`.
2. Hashes the new password via `PasswordHasher<User>`.
3. Marks `EmailVerification.VerifiedAt = DateTime.UtcNow`.
4. **Session Invalidation:** Queries all non-revoked refresh tokens for the user in `RefreshTokens` and sets `IsRevoked = true`, ensuring any existing sessions (or potentially compromised devices) are terminated.

---

## 4. Token & Security Specifications

### 4.1 JSON Web Token (JWT) Structure
- **Algorithm:** HMAC-SHA256 (`SecurityAlgorithms.HmacSha256`).
- **Signature Key:** Configured via `JwtSettings:Secret` with a fallback key.
- **Issuer (`iss`):** Configured via `JwtSettings:Issuer` (Default: `"DevPilotAI"`).
- **Audience (`aud`):** Configured via `JwtSettings:Audience` (Default: `"DevPilotAIUsers"`).
- **Expiration (`exp`):** Configurable in minutes via `JwtSettings:ExpiryMinutes` (Default: 60 to 120 minutes).
- **Clock Skew:** Configured to `TimeSpan.Zero` for exact boundary verification.

#### Embedded Claims
| Claim | Standard Key | Value / Source |
| :--- | :--- | :--- |
| Subject | `sub` | `User.UserId.ToString()` |
| Name Identifier | `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` | `User.UserId.ToString()` |
| Email | `email` / `ClaimTypes.Email` | `User.Email` |
| Name | `name` / `ClaimTypes.Name` | `User.Name` |
| JWT ID | `jti` | Unique `Guid.NewGuid().ToString()` |
| Role | `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` | User role(s) (`Developer`, `Administrator`) |

### 4.2 Refresh Token Lifecycle & Schema
- **Generation:** 32 cryptographically random bytes via `RandomNumberGenerator.Fill(byte[])`, base64url-encoded.
- **Storage:** Persisted as SHA-256 hash in `RefreshTokens.TokenHash`.
- **Database Schema (`RefreshTokens`):**
  - `RefreshTokenId` (INT, Primary Key)
  - `UserId` (INT, Foreign Key to `Users.UserId`)
  - `TokenHash` (NVARCHAR(255), Indexed)
  - `ReplacedByTokenHash` (NVARCHAR(255), NULL)
  - `ExpiresAt` (DATETIME2, UTC)
  - `CreatedAt` (DATETIME2, UTC)
  - `IsRevoked` (BIT, Default 0)
  - `RevokedAt` (DATETIME2, NULL)
  - `IsUsed` (BIT, Default 0)

### 4.3 Password Hashing
- Utilizes `Microsoft.AspNetCore.Identity.PasswordHasher<User>`.
- Employs PBKDF2 with HMAC-SHA512, per-user cryptographic salt, and 100,000+ iterations.
- Never stores or compares passwords using generic unsalted or raw SHA-256 hashes.

### 4.4 Bot Protection (Turnstile)
- Cloudflare Turnstile token verification is integrated into `POST /api/auth/register`.
- Validated server-side via `ITurnstileService` before user persistence.
- Rejection produces an HTTP 409 Conflict error.

---

## 5. Authentication Endpoint Summary Matrix

| HTTP Method | Endpoint | Access Level | Primary Jira Task | Success Status | Key Behavior / Response |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/register` | Public | **DA-34** / DA-33 | `201 Created` | Creates account with `Developer` role; sends 15-min OTP. |
| `POST` | `/api/auth/verify-email` | Public | **DA-33** | `200 OK` | Validates OTP/link token; unlocks account for login. |
| `POST` | `/api/auth/resend-verification` | Public | **DA-33** | `200 OK` | Resends OTP; enforces 60-second cooldown. |
| `POST` | `/api/auth/login` | Public | **DA-34** | `200 OK` | Authenticates user; issues JWT & 7-day Refresh Token. |
| `POST` | `/api/auth/refresh` | Refresh Token | **DA-34** | `200 OK` | Rotates refresh token; issues new JWT; detects reuse. |
| `POST` | `/api/auth/logout` | Refresh Token | **DA-34** | `200 OK` | Revokes refresh token in database. |
| `POST` | `/api/auth/forgot-password` | Public | **DA-33** | `200 OK` | Issues 30-min reset link; enumeration protected. |
| `POST` | `/api/auth/reset-password` | Public | **DA-33** | `200 OK` | Updates password; revokes all active sessions. |

---

## 6. Brevo Transactional Email Integration

### 6.1 Architectural Design
DevPilot AI employs Brevo's v3 Transactional Email API (`POST https://api.brevo.com/v3/smtp/email`) for mission-critical notifications. In adherence to Clean Architecture:
- **Application Layer (`IEmailService`):** Completely decoupled from provider implementations and HTTP mechanisms. Defines clean asynchronous contracts (`SendVerificationEmailAsync`, `SendPasswordResetEmailAsync`).
- **Infrastructure Layer (`BrevoEmailService`):** Manages HTTP communication via typed `HttpClient`, authenticates requests via the `api-key` header, loads HTML email templates, and injects securely encoded parameters.
- **Authentication Service (`AuthService`):** Orchestrates database persistence first, then triggers email dispatch, ensuring that raw OTPs and reset tokens are dispatched directly via email and never stored or logged.

### 6.2 Sender & Configuration Parameters
Brevo requests are sent from the verified account sender:

| Configuration Property | Environment Variable | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Brevo:ApiKey` | `BREVO__APIKEY` | *(Must be supplied)* | Brevo API key. Never committed to source control. |
| `Brevo:SenderEmail` | `BREVO__SENDEREMAIL` | `midohema94@gmail.com` | Verified Brevo sender email address. |
| `Brevo:SenderName` | `BREVO__SENDERNAME` | `DevPilot` | Display name of the sender. |
| `Brevo:BaseUrl` | `BREVO__BASEURL` | `https://api.brevo.com` | Brevo API base endpoint. |
| `Frontend:BaseUrl` | `FRONTEND__BASEURL` | `https://devpilot.ai` | Trusted frontend domain for password reset links. |

### 6.3 Automated Email Flows
1. **Developer Registration (`POST /api/auth/register`):**
   - Renders `VerificationEmail.html`.
   - Embeds a 6-digit numeric OTP valid for 15 minutes.
   - Dispatches to the registered developer's email address.
2. **Resend Verification (`POST /api/auth/resend-verification`):**
   - Invalidates previous unverified OTP records, generates a new OTP, and re-dispatches `VerificationEmail.html`.
   - Strictly protected by a 60-second cooldown window.
3. **Password Recovery (`POST /api/auth/forgot-password`):**
   - Renders `PasswordResetEmail.html`.
   - Embeds a single-use cryptographic reset link (`<Frontend:BaseUrl>/reset-password?token=<ResetToken>`) valid for 30 minutes.
   - Dispatches only if the user exists, maintaining generic account enumeration responses to callers.

### 6.4 Security Controls & Sanitization
- **Credential Protection:** The API key is injected via environment variables or secret managers, never present in git or source code.
- **HTML Injection Defense:** All user inputs (`recipientName`, `otpCode`, `resetUrl`) undergo strict HTML encoding via `WebUtility.HtmlEncode` before template injection.
- **SSRF / Host Header Injection Prevention:** Password reset URLs are constructed solely from the configured `Frontend:BaseUrl`, ignoring any client-supplied `Host`, `X-Forwarded-Host`, or `Referer` headers.
- **Zero Raw Logging:** Sensitive credentials, passwords, raw OTPs, and reset tokens are excluded from all logging pipelines.

### 6.5 Local Development & Manual Testing
1. Configure your local Brevo API key using .NET User Secrets or environment variable:
   ```bash
   # Option A: User Secrets (from DevPilotAI.API directory)
   dotnet user-secrets set "Brevo:ApiKey" "<YOUR_BREVO_API_KEY>"

   # Option B: Environment Variable (PowerShell)
   $env:BREVO__APIKEY="<YOUR_BREVO_API_KEY>"
   ```
2. Start the API application:
   ```bash
   dotnet run --project src/DevPilotAI.API
   ```
3. Test Account Registration:
   ```bash
   curl -X POST https://localhost:7001/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{"name":"Tester","email":"your-email@example.com","password":"Password123"}'
   ```
4. Check your inbox for the DevPilot AI verification email and copy the 6-digit OTP.
5. Complete account verification:
   ```bash
   curl -X POST https://localhost:7001/api/auth/verify-email \
     -H "Content-Type: application/json" \
     -d '{"email":"your-email@example.com","code":"<6_DIGIT_OTP>"}'
   ```

