# DevPilot AI — Authentication API Documentation & Integration Specification

This document provides a complete, reality-based specification for all **8 Authentication API Endpoints** implemented in the DevPilot AI backend.

For every endpoint, this document explicitly details:
1. **Purpose of API**: What the endpoint does, its architectural purpose, and security behavior.
2. **Request Body**: Complete field breakdown, data types, constraints, and JSON request payload example.
3. **Response Body**: HTTP status codes, envelope wrappers, data fields, and JSON response payload example.
4. **Error Handling Messages**: Explicitly stated as **FOUND** (with exact error text, error codes, and HTTP statuses) or **NOT FOUND** if none apply.
5. **User Experience (UX) Messages**: Explicitly stated as **FOUND** (with exact user-facing feedback messages returned by the API and actionable frontend recommendations) or **NOT FOUND** if none exist.

---

## Global API Standards & Conventions

### 1. Base URL
- **Production Base URL:** `https://<your-monsterasp-domain>/api/auth`
- **Local Development URL:** `https://localhost:7129/api/auth` (or `http://localhost:5281/api/auth`)

### 2. Standard Envelopes

#### Success Envelope (HTTP 200 OK / HTTP 201 Created)
Every successful API call is wrapped in a consistent top-level JSON envelope:
```json
{
  "success": true,
  "data": { ... }
}
```

#### Error Envelope (HTTP 400 / 401 / 404 / 409 / 429 / 500)
All API exceptions and validation failures are intercepted by `ExceptionHandlingMiddleware` (or the rate limiter) and return a standardized error envelope:
```json
{
  "success": false,
  "errorCode": "BAD_REQUEST | UNAUTHORIZED | NOT_FOUND | CONFLICT | RATE_LIMIT_EXCEEDED | INTERNAL_SERVER_ERROR",
  "message": "Descriptive, user-safe error message",
  "requestId": "0HN1234567890:00000001"
}
```

---

## Summary of All Authentication Endpoints

| # | HTTP Method | Endpoint Route | Purpose | Rate Limited | Auth Required |
| :-: | :--- | :--- | :--- | :-: | :-: |
| 1 | `POST` | `/api/auth/register` | Create developer account, assign Free plan & role, send verification OTP | **YES** (5 per 300s) | None (Public) |
| 2 | `POST` | `/api/auth/verify-email` | Verify account email using 6-digit OTP code or cryptographic link token | No | None (Public) |
| 3 | `POST` | `/api/auth/resend-verification` | Resend a fresh 6-digit OTP verification code to user's email | No | None (Public) |
| 4 | `POST` | `/api/auth/login` | Authenticate credentials, issue HMAC-SHA256 JWT and refresh token | **YES** (5 per 300s) | None (Public) |
| 5 | `POST` | `/api/auth/refresh` | Rotate cryptographic refresh token and issue fresh access token | No | Refresh Token |
| 6 | `POST` | `/api/auth/logout` | Revoke active refresh token session | No | Refresh Token |
| 7 | `POST` | `/api/auth/forgot-password` | Request password reset email link with account enumeration protection | No | None (Public) |
| 8 | `POST` | `/api/auth/reset-password` | Set new password using verified reset token, revoking prior sessions | No | None (Public) |

---

## 1. Register API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/register`
- **Access Level:** Public (Unauthenticated)
- **Rate Limit Policy:** `Register` (Fixed window: 5 requests per 300 seconds partitioned by Client IP)

### Purpose of API
Registers a new developer account into DevPilot AI:
- Verifies optional Cloudflare Turnstile bot protection.
- Validates name, email, and strong password constraints.
- Hashes password using PBKDF2/HMAC-SHA512 (`PasswordHasher<User>`).
- Provisions the user record with `EmailVerifiedAt = NULL`.
- Assigns the default `"Developer"` role in `UserRoles`.
- Automatically activates the default Free subscription (`PlanId = 1`, `CreditsPerPeriod = 100`).
- Generates a cryptographically random 6-digit OTP code (valid for 15 minutes, stored as SHA-256 hash).
- Sends a verification email to the user via Brevo Transactional Email API.
- Emits a security audit log event (`AccountCreated`).

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `name` | string | **YES** | Min: 2, Max: 150 chars | Full name of the developer. |
| `email` | string | **YES** | Valid email format, Max: 255 chars | Account email. Normalized to lowercase. |
| `password` | string | **YES** | Min: 8, Max: 100 chars. Regex: $\ge 1$ uppercase, $\ge 1$ lowercase, $\ge 1$ digit | Plaintext account password. |
| `turnstileToken` | string | Optional | Nullable string | Cloudflare Turnstile response token from frontend widget. |

#### Example Request Body
```json
{
  "name": "Mahmoud Ahmed",
  "email": "developer@example.com",
  "password": "SecurePassword123!",
  "turnstileToken": "0.AbCdEf123456789..."
}
```

### Response Body
- **HTTP Status:** `201 Created`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (always `true` on success). |
| `data.success` | boolean | Registration operation status confirmation. |
| `data.message` | string | User-facing notification message. |
| `data.email` | string | The registered email address. |
| `data.requiresVerification` | boolean | Indicates whether the account requires email verification before login (`true`). |

#### Example Response Body
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

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing name field | `"Name is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Name length < 2 or > 150 chars | `"Name must be between 2 and 150 characters."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing email field | `"Email is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Malformed email syntax | `"Invalid email format."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Email length > 255 chars | `"Email cannot exceed 255 characters."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing password field | `"Password is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Password length < 8 chars | `"Password must be at least 8 characters long."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Password missing required character classes | `"Password must contain at least one uppercase letter, one lowercase letter, and one number."` |
| **`409 Conflict`** | `CONFLICT` | Cloudflare Turnstile token validation fails | `"Security check failed. Please complete the captcha verification."` |
| **`409 Conflict`** | `CONFLICT` | Email already registered in database | `"An account with this email address already exists."` |
| **`429 Too Many Requests`** | `RATE_LIMIT_EXCEEDED` | Exceeded 5 registration attempts in 5 minutes from same IP | `"Too many requests. Please try again later."` |

### User Experience (UX) Messages

#### Status: FOUND
- **API Returned Message:**
  `"Registration successful. A verification code has been sent to your email."`
- **Recommended Frontend UX Behavior:**
  1. Display a positive banner or toast with the message: *"Registration successful. A verification code has been sent to your email."*
  2. Automatically transition the user to the Email Verification screen with their email pre-filled.
  3. Inform the user to check their spam/junk folder if the email does not arrive within 1 minute.

---

## 2. Verify Email API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/verify-email`
- **Access Level:** Public (Unauthenticated)
- **Rate Limit Policy:** General

### Purpose of API
Validates the user's email address and activates the account:
- Supports two verification mechanisms:
  1. **6-Digit OTP Code**: Supplied alongside the user's `email`.
  2. **Cryptographic Link Token**: Extracted from verification email links (automatically handles raw tokens or full URL parameters).
- Tracks failed verification attempts; enforces a strict security limit of maximum 5 failed attempts per OTP.
- On successful verification, records `EmailVerifiedAt = DateTime.UtcNow`, marks the verification record as verified, and logs a security audit event (`EmailVerified`).

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `email` | string | Conditional | Required if verifying via OTP `code` | User's registered email address. |
| `code` | string | Conditional | Max: 20 chars (6-digit numeric string) | The OTP code received by the user via email. |
| `token` | string | Conditional | Valid token string | The verification token received in verification link. |

*Note: At least one of `code` or `token` must be provided.*

#### Example Request Body (OTP Method)
```json
{
  "email": "developer@example.com",
  "code": "482910"
}
```

#### Example Request Body (Link Token Method)
```json
{
  "token": "verify_1_aBcDeF1234567890..."
}
```

### Response Body
- **HTTP Status:** `200 OK`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (`true`). |
| `data.success` | boolean | Verification confirmation status (`true`). |
| `data.message` | string | Confirmation message stating email was verified. |

#### Example Response Body
```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Email verified successfully."
  }
}
```

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | Neither `code` nor `token` provided in body | `"Either verification code or verification token must be provided."` |
| **`400 Bad Request`** | `BAD_REQUEST` | `code` provided but `email` is null or empty | `"Email is required when verifying with a code."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Invalid email format | `"Invalid email format."` |
| **`400 Bad Request`** | `BAD_REQUEST` | `code` exceeds 20 characters | `"Verification code cannot exceed 20 characters."` |
| **`404 Not Found`** | `NOT_FOUND` | No user exists for the given email, or user account is soft-deleted | `"User account not found."` |
| **`409 Conflict`** | `CONFLICT` | Email was already verified previously | `"Email address is already verified."` |
| **`409 Conflict`** | `CONFLICT` | Wrong OTP code, expired OTP (>15 mins), or $\ge 5$ failed attempts | `"Invalid or expired verification code."` |
| **`409 Conflict`** | `CONFLICT` | Verification link token is invalid, expired, or user deleted | `"Invalid or expired verification token."` |

### User Experience (UX) Messages

#### Status: FOUND
- **API Returned Message:**
  `"Email verified successfully."`
- **Recommended Frontend UX Behavior:**
  1. Display a prominent green success alert: *"Email verified successfully."*
  2. Provide a 3-second automatic redirect (or a *"Proceed to Login"* button) taking the user directly to the Login page.
  3. If an `"Invalid or expired verification code."` error occurs, present an option to request a new code via `/api/auth/resend-verification`.

---

## 3. Resend Verification API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/resend-verification`
- **Access Level:** Public (Unauthenticated)
- **Rate Limit Policy:** General

### Purpose of API
Dispatches a new 6-digit email verification OTP to unverified accounts:
- Verifies that the user account exists and is not soft-deleted.
- Confirms the account is not already verified.
- Enforces an automated **60-second rate-limiting cooldown** between resend requests to prevent mailbox flooding and API abuse.
- Invalidates prior OTPs and generates a fresh 15-minute OTP hash.
- Dispatches the verification email through Brevo.

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `email` | string | **YES** | Valid email format | The registered email address awaiting verification. |

#### Example Request Body
```json
{
  "email": "developer@example.com"
}
```

### Response Body
- **HTTP Status:** `200 OK`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (`true`). |
| `data.success` | boolean | Resend operation confirmation status (`true`). |
| `data.message` | string | User-facing confirmation message. |

#### Example Response Body
```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Verification code has been resent to your email."
  }
}
```

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | Email property omitted | `"Email is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Malformed email syntax | `"Invalid email format."` |
| **`404 Not Found`** | `NOT_FOUND` | No user found for the email, or account is soft-deleted | `"User account not found."` |
| **`409 Conflict`** | `CONFLICT` | Account email is already verified | `"This account is already verified."` |
| **`409 Conflict`** | `CONFLICT` | Resend requested within 60 seconds of last code creation | `"Please wait before requesting another verification code."` |

### User Experience (UX) Messages

#### Status: FOUND
- **API Returned Message:**
  `"Verification code has been resent to your email."`
- **Recommended Frontend UX Behavior:**
  1. Display a toast notification: *"Verification code has been resent to your email."*
  2. Disable the "Resend Code" button and trigger a 60-second visual countdown timer to prevent triggering the 409 cooldown error.

---

## 4. Login API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/login`
- **Access Level:** Public (Unauthenticated)
- **Rate Limit Policy:** `Login` (Fixed window: 5 requests per 300 seconds partitioned by Client IP)

### Purpose of API
Authenticates user credentials and initiates an authorized session:
- Validates email and password syntax.
- Verifies hashed password against PBKDF2/HMAC-SHA512 hash.
- Enforces mandatory email verification (blocks unverified users).
- Blocks soft-deleted or deactivated users.
- Issues a signed HMAC-SHA256 JWT Access Token carrying user identity (`sub`, `email`, `name`, `jti`) and role claims (`Role: Developer`, `Role: Administrator`).
- Issues a cryptographically secure 256-bit entropy Refresh Token (stored as SHA-256 hash, valid for 7 days).
- Emits a security audit log event (`LoginSuccess` on success, or `LoginFailed` on failure with sanitized diagnostic reason).

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `email` | string | **YES** | Valid email format | Registered account email address. |
| `password` | string | **YES** | Non-empty string | Plaintext account password. |

#### Example Request Body
```json
{
  "email": "developer@example.com",
  "password": "SecurePassword123!"
}
```

### Response Body
- **HTTP Status:** `200 OK`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (`true`). |
| `data.success` | boolean | Login operation confirmation (`true`). |
| `data.accessToken` | string | Cryptographically signed HMAC-SHA256 JWT access token. |
| `data.refreshToken` | string | Opaque 256-bit entropy refresh token for session maintenance. |
| `data.user.userId` | integer | Unique identifier of the authenticated user. |
| `data.user.name` | string | Display name of the user. |
| `data.user.email` | string | Registered email address. |
| `data.user.emailVerifiedAt` | string (ISO 8601) | Timestamp of email verification completion. |

#### Example Response Body
```json
{
  "success": true,
  "data": {
    "success": true,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwibmFtZWlkZW50aWZpZXIiOiIxIiwiZW1haWwiOiJkZXZlbG9wZXJAZXhhbXBsZS5jb20iLCJuYW1lIjoiTWFobW91ZCBBaG1lZCIsImp0aSI6ImE1M2Y4OTcxLi4uIiwiUm9sZSI6WyJEZXZlbG9wZXIiXSwiaXNzIjoiRGV2UGlsb3RBSSIsImF1ZCI6IkRldlBpbG90QUlVc2VycyIsImV4cCI6MTc2MDAwMDAwMH0.signature...",
    "refreshToken": "refresh_1_7F3b9C2q5xK8vM1wP4nZ0rT...",
    "user": {
      "userId": 1,
      "name": "Mahmoud Ahmed",
      "email": "developer@example.com",
      "emailVerifiedAt": "2026-10-09T14:00:00Z"
    }
  }
}
```

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing email in payload | `"Email is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Malformed email address | `"Invalid email format."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing password in payload | `"Password is required."` |
| **`401 Unauthorized`** | `UNAUTHORIZED` | User not found, account soft-deleted, or password incorrect | `"Invalid email or password."` |
| **`401 Unauthorized`** | `UNAUTHORIZED` | Account credentials correct, but email not verified | `"Email is not verified. Please verify your account before logging in."` |
| **`429 Too Many Requests`** | `RATE_LIMIT_EXCEEDED` | Exceeded 5 login attempts in 5 minutes from same IP | `"Too many requests. Please try again later."` |

### User Experience (UX) Messages

#### Status: NOT FOUND (In Response Payload) / FOUND (In Error Context)
- **API Returned Success Message:**
  **NOT FOUND** — The Login API does not return a generic `"message"` text string in the response payload. It directly returns the token pair and user profile object.
- **API Returned Error Messages (User-Facing):**
  - `"Invalid email or password."`
  - `"Email is not verified. Please verify your account before logging in."`
- **Recommended Frontend UX Behavior:**
  1. Store the `accessToken` in memory or secure storage, and store `refreshToken` securely.
  2. Greet the developer using their name: *"Welcome back, {user.name}!"*
  3. If the error `"Email is not verified. Please verify your account before logging in."` is received, display a notification with a direct link: *"Your account is not verified yet. Click here to verify your email."*

---

## 5. Refresh Token API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/refresh`
- **Access Level:** Refresh Token Authenticated
- **Rate Limit Policy:** General

### Purpose of API
Maintains seamless user sessions via cryptographic Token Rotation:
- Accepts an existing refresh token.
- Verifies the token's SHA-256 hash against `RefreshTokens` in the database.
- Confirms the token is not revoked, not expired, and belongs to an active user.
- **Automated Reuse Detection (Session Hijacking Protection):** If a token marked as already used (`IsUsed = true`) is presented, the system detects a potential breach, immediately revokes **all** active refresh tokens for that user, logs a security audit event (`RefreshTokenReuse`), and returns HTTP 401.
- On valid exchange: marks the old token as used (`IsUsed = true`, `ReplacedByTokenHash = newHash`), creates a brand-new 7-day refresh token, generates a new JWT access token, and returns both to the client.

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `refreshToken` | string | **YES** | Non-empty string | The current active refresh token string. |

#### Example Request Body
```json
{
  "refreshToken": "refresh_1_7F3b9C2q5xK8vM1wP4nZ0rT..."
}
```

### Response Body
- **HTTP Status:** `200 OK`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (`true`). |
| `data.success` | boolean | Refresh operation confirmation (`true`). |
| `data.accessToken` | string | Brand-new signed HMAC-SHA256 JWT access token. |
| `data.refreshToken` | string | Brand-new cryptographic refresh token for the next rotation cycle. |

#### Example Response Body
```json
{
  "success": true,
  "data": {
    "success": true,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "refresh_1_9X2kLm4pQ7vRtW3yU8zB1cA..."
  }
}
```

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | `refreshToken` property is omitted or whitespace | `"Refresh token is required."` |
| **`401 Unauthorized`** | `UNAUTHORIZED` | Token not found, revoked, expired, or user deleted | `"Invalid or expired refresh token."` |
| **`401 Unauthorized`** | `UNAUTHORIZED` | Token was already rotated and used previously | `"Refresh token reuse detected. All active sessions have been revoked for security."` |

### User Experience (UX) Messages

#### Status: NOT FOUND (In Response Payload) / FOUND (In Error Context)
- **API Returned Success Message:**
  **NOT FOUND** — The Refresh Token API operates silently in the background and does not return a text `"message"` string in the response payload.
- **Recommended Frontend UX Behavior:**
  1. The frontend HTTP interceptor (e.g. Axios or Fetch wrapper) should silently intercept expired 401 responses, call `/api/auth/refresh`, and swap the tokens without interrupting the user.
  2. If the API returns 401 (`"Refresh token reuse detected..."` or `"Invalid or expired refresh token."`), clear local auth state and redirect the user to the Login page with a notification: *"Your session has expired. Please log in again."*

---

## 6. Logout API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/logout`
- **Access Level:** Refresh Token Authenticated
- **Rate Limit Policy:** General

### Purpose of API
Terminates the user's active session:
- Accepts the current refresh token.
- Hashes the token and locates the record in `RefreshTokens`.
- Sets `IsRevoked = true` and `RevokedAt = DateTime.UtcNow`.
- Emits a security audit log event (`Logout`).
- **Idempotent Design:** If the token is already revoked or missing, the endpoint gracefully succeeds to ensure clients can always log out cleanly without errors.

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `refreshToken` | string | **YES** | Non-empty string | The refresh token associated with the session being terminated. |

#### Example Request Body
```json
{
  "refreshToken": "refresh_1_9X2kLm4pQ7vRtW3yU8zB1cA..."
}
```

### Response Body
- **HTTP Status:** `200 OK`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (`true`). |
| `data.success` | boolean | Logout operation confirmation (`true`). |
| `data.message` | string | User-facing confirmation message. |

#### Example Response Body
```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Logged out successfully."
  }
}
```

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | `refreshToken` property is omitted or whitespace | `"Refresh token is required."` |

*Note: Invalid or expired tokens do NOT trigger an error on logout; the endpoint returns 200 OK to ensure a clean logout experience.*

### User Experience (UX) Messages

#### Status: FOUND
- **API Returned Message:**
  `"Logged out successfully."`
- **Recommended Frontend UX Behavior:**
  1. Purge all stored tokens and cached user data from browser storage / cookies.
  2. Display a brief notification: *"Logged out successfully."*
  3. Redirect user to the Login page or Public Landing page.

---

## 7. Forgot Password API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/forgot-password`
- **Access Level:** Public (Unauthenticated)
- **Rate Limit Policy:** General

### Purpose of API
Initiates a password recovery workflow:
- Validates email syntax.
- Searches for the user account.
- **Account Enumeration Defense:** If the email does **not** exist or belongs to a soft-deleted user, the API **does not reveal this information**. It returns the exact same success response message to prevent attackers from discovering registered email addresses.
- If the account exists:
  - Generates a secure, cryptographically random password reset token.
  - Stores a SHA-256 hash of the token in `EmailVerifications` with `Method = "ResetLink"` and a 30-minute expiration window (`ExpiresAt = DateTime.UtcNow.AddMinutes(30)`).
  - Sends a password recovery email containing the reset link (`{FrontendUrl}/reset-password?token={Token}`) via Brevo.

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `email` | string | **YES** | Valid email format | The email address associated with the account. |

#### Example Request Body
```json
{
  "email": "developer@example.com"
}
```

### Response Body
- **HTTP Status:** `200 OK`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (`true`). |
| `data.success` | boolean | Operation confirmation (`true`). |
| `data.message` | string | Enumeration-safe user confirmation message. |

#### Example Response Body
```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "If the email is registered, a password reset link has been sent to your email address."
  }
}
```

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing email in payload | `"Email is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Malformed email address | `"Invalid email format."` |

*Note: Non-existent emails do NOT produce a 404 error; they deliberately return 200 OK for account enumeration protection.*

### User Experience (UX) Messages

#### Status: FOUND
- **API Returned Message:**
  `"If the email is registered, a password reset link has been sent to your email address."`
- **Recommended Frontend UX Behavior:**
  1. Display an informational message banner: *"If the email is registered, a password reset link has been sent to your email address."*
  2. Advise the user: *"Please check your inbox and spam folder. The password reset link will expire in 30 minutes."*

---

## 8. Reset Password API

### Endpoint
- **Method:** `POST`
- **Route:** `/api/auth/reset-password`
- **Access Level:** Public (Unauthenticated)
- **Rate Limit Policy:** General

### Purpose of API
Completes the password recovery workflow:
- Validates the reset token and password complexity constraints.
- Automatically extracts and unescapes tokens even if clients submit full URLs containing `token=...`.
- Verifies that the token hash exists in `EmailVerifications`, is unverified, is unexpired (< 30 minutes), and has not exceeded 5 attempts.
- Hashes the new password using PBKDF2/HMAC-SHA512 (`PasswordHasher<User>`) and updates the user's `PasswordHash`.
- Marks the reset token as verified (`VerifiedAt = DateTime.UtcNow`).
- **Global Session Revocation:** Immediately revokes **all** existing active refresh tokens for the user to force all devices and stale sessions to re-authenticate with the new password.
- Emits a security audit log event (`PasswordReset`).

### Request Body

#### Schema & Constraints
| Field | Type | Required | Constraints | Description |
| :--- | :--- | :---: | :--- | :--- |
| `token` | string | **YES** | Non-empty string | The reset token received from the password recovery email. |
| `newPassword` | string | **YES** | Min: 8, Max: 100 chars. Regex: $\ge 1$ uppercase, $\ge 1$ lowercase, $\ge 1$ digit | The new password to set for the account. |

#### Example Request Body
```json
{
  "token": "reset_1_8K3mP0qW4yT9vL2zB5xR7aC...",
  "newPassword": "NewStrongPassword2026!"
}
```

### Response Body
- **HTTP Status:** `200 OK`

#### Schema Breakdown
| Field | Type | Description |
| :--- | :--- | :--- |
| `success` | boolean | Top-level request status flag (`true`). |
| `data.success` | boolean | Password reset confirmation (`true`). |
| `data.message` | string | User-facing confirmation message. |

#### Example Response Body
```json
{
  "success": true,
  "data": {
    "success": true,
    "message": "Password reset successfully. You can now log in with your new password."
  }
}
```

### Error Handling Messages

#### Status: FOUND

| HTTP Status | Error Code (`errorCode`) | Trigger Condition | Exact Error Message |
| :--- | :--- | :--- | :--- |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing token in payload | `"Reset token is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | Missing new password field | `"New password is required."` |
| **`400 Bad Request`** | `BAD_REQUEST` | New password length < 8 chars | `"Password must be at least 8 characters long."` |
| **`400 Bad Request`** | `BAD_REQUEST` | New password missing required character classes | `"Password must contain at least one uppercase letter, one lowercase letter, and one number."` |
| **`409 Conflict`** | `CONFLICT` | Token hash not found, already verified, expired (>30m), $\ge 5$ attempts, or user deleted | `"Invalid or expired reset token."` |

### User Experience (UX) Messages

#### Status: FOUND
- **API Returned Message:**
  `"Password reset successfully. You can now log in with your new password."`
- **Recommended Frontend UX Behavior:**
  1. Display a prominent green success alert: *"Password reset successfully. You can now log in with your new password."*
  2. Automatically redirect the user to the Login page after 2–3 seconds.
  3. If an `"Invalid or expired reset token."` error occurs, instruct the user to initiate a new password reset request from the Forgot Password page.

---

## Comprehensive UX Messages Summary Matrix

For fast frontend UI development, here is a consolidated reference of every user experience message provided by the authentication API:

| API Endpoint | Response Type | Message Status | Exact Message String Returned by API | Recommended UI Action |
| :--- | :---: | :---: | :--- | :--- |
| `POST /api/auth/register` | Success Payload | **FOUND** | `"Registration successful. A verification code has been sent to your email."` | Show success alert; route to OTP verification screen. |
| `POST /api/auth/verify-email` | Success Payload | **FOUND** | `"Email verified successfully."` | Show success toast; redirect to `/login`. |
| `POST /api/auth/resend-verification` | Success Payload | **FOUND** | `"Verification code has been resent to your email."` | Show toast; activate 60s cooldown timer on resend button. |
| `POST /api/auth/login` | Success Payload | **NOT FOUND** | *(None — returns JWT tokens and user profile)* | Store tokens; greet `{user.name}`; navigate to dashboard. |
| `POST /api/auth/login` | Error (401) | **FOUND** | `"Invalid email or password."` | Highlight form error; prompt user to check credentials. |
| `POST /api/auth/login` | Error (401) | **FOUND** | `"Email is not verified. Please verify your account before logging in."` | Show warning banner with link to OTP verification. |
| `POST /api/auth/refresh` | Success Payload | **NOT FOUND** | *(None — silent token rotation)* | Silently update cached tokens in background. |
| `POST /api/auth/refresh` | Error (401) | **FOUND** | `"Refresh token reuse detected. All active sessions have been revoked for security."` | Clear local storage; force user to log in again. |
| `POST /api/auth/logout` | Success Payload | **FOUND** | `"Logged out successfully."` | Clear stored credentials; navigate to login. |
| `POST /api/auth/forgot-password` | Success Payload | **FOUND** | `"If the email is registered, a password reset link has been sent to your email address."` | Display info banner to check inbox and spam folders. |
| `POST /api/auth/reset-password` | Success Payload | **FOUND** | `"Password reset successfully. You can now log in with your new password."` | Show success alert; navigate to login page. |
| `Any Endpoint` | Error (429) | **FOUND** | `"Too many requests. Please try again later."` | Display throttle alert with retry duration from `Retry-After` header. |
