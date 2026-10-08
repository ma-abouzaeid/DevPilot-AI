# Soft Delete Architecture Specification

**Project**: DevPilot AI  
**Author**: Backend Engineering Team  
**Scope**: Domain Entities, Persistence Layer, Security Protocols, and Query Infrastructure  

---

## 1. What Soft Delete Means in DevPilot AI

In DevPilot AI, Soft Delete is an architectural pattern applied strictly to entities representing user-owned business data that can be logically removed or archived from active user views, while the underlying relational records, history, and audit metadata must be permanently preserved in SQL Server.

When an entity is soft-deleted:
- The physical database row is **never removed**.
- `IsDeleted` is updated from `false` (`0`) to `true` (`1`).
- `DeletedAt` is stamped with the current UTC timestamp (`DateTime.UtcNow`).
- EF Core **Global Query Filters** automatically hide the row from standard LINQ queries, projections, and navigation inclusions across all application services and API controllers.
- The row remains queryable only via explicit administrative or security routines employing `.IgnoreQueryFilters()`.

Crucially, **Soft Delete is not applied universally to all database tables**. It is reserved for entities where logical retirement represents a valid business state and preserving historical records is required.

---

## 2. Entities That Use Soft Delete

The following 8 entities implement the `ISoftDeletable` contract (`IsDeleted`, `DeletedAt`):

1. **`User` (`Users`)**: User accounts. Soft-deleted when a developer initiates `DELETE /api/users/me`.
2. **`Problem` (`Problems`)**: Developer problem tickets. Soft-deleted when a user deletes a problem from their dashboard.
3. **`ProblemAttachment` (`ProblemAttachments`)**: Uploaded logs/code files. Soft-deleted when a user removes an attachment from a request.
4. **`ProblemSolution` (`ProblemSolutions`)**: Curated solutions. Soft-deleted if deprecated or replaced, preserving historical problem solving data.
5. **`KnowledgeArticle` (`KnowledgeArticles`)**: Knowledge articles. Soft-deleted when unpublished or removed from public documentation catalogs.
6. **`Challenge` (`Challenges`)**: Practice challenges. Soft-deleted when retired from the challenge catalog.
7. **`GitHubRepository` (`GitHubRepositories`)**: Imported developer repositories. Soft-deleted when unlinked from the active workspace.
8. **`CodeReview` (`CodeReviews`)**: Code review runs. Soft-deleted when dismissed or archived by the developer.

---

## 3. Entities That Do NOT Use Soft Delete

The following 15 actual EF Core entities intentionally do **not** implement soft delete:

1. **`Role`**: Static system reference data (`Developer`, `Administrator`).
2. **`UserRole`**: Relational junction table linking users to roles.
3. **`RefreshToken`**: Cryptographic session security data governed by `IsRevoked`, `RevokedAt`, `IsUsed`, and `ExpiresAt`.
4. **`EmailVerification`**: Single-use security OTP/link data governed by `VerifiedAt`, `ExpiresAt`, and `Attempts`.
5. **`ProblemRequest`**: Immutable snapshot of developer inputs (source code, logs, stack traces) within a debugging session.
6. **`DebuggingAttempt`**: Immutable AI reasoning iterations, root-cause analyses, and suggested patches.
7. **`DeveloperFeedback`**: Immutable evaluation logs (Success/Failure/Partial) on debugging attempts.
8. **`ArticleVersion`**: Immutable historical revisions of knowledge articles.
9. **`ChallengeAttempt`**: Immutable historical records of developer submissions.
10. **`ChallengeEvaluation`**: Immutable evaluation reports and test scores.
11. **`GitHubAccount`**: OAuth integration entity managed by connection/credential lifecycle.
12. **`CodeReviewFinding`**: Immutable lint/security findings belonging to a code review.
13. **`AIProvider`**: Infrastructure configuration managed by `ProviderStatus` (`Active`, `Inactive`, `RateLimited`, `Degraded`).
14. **`AIModel`**: Provider model configuration managed by `IsActive`.
15. **`AIRequest`**: Append-only telemetry and token usage audit ledger.

---

## 4. Architectural Categorization Rationale

Every entity belongs to one of six architectural categories:

| Category | Semantics | Strategy | Rationale |
|---|---|---|---|
| **A. Soft Deletable** | User/Business Data | `ISoftDeletable` (`IsDeleted`, `DeletedAt`) | High-level entities that users or admins can dismiss, but which possess dependent historical children that must remain intact. |
| **B. Lifecycle/Status Based** | Operational State | Domain Status / Enums / Flags | State transitions (e.g. `ProviderStatus`, `IsActive`, `GitHubAccount.ConnectedAt`) accurately represent business validity without synthetic delete semantics. |
| **C. Append-Only / Historical** | Telemetry / Audits / History | Immutable Retention | Events and historical versions (e.g. `AIRequest`, `DebuggingAttempt`, `ArticleVersion`, `DeveloperFeedback`) must remain permanently unchanged for compliance and machine learning feedback loops. |
| **D. System / Reference Data** | Core Master Data | Permanent Reference | System-defined reference tables (e.g. `Role`, `Technology`) must never be deleted by normal user operations. |
| **E. Security / Session Data** | Cryptographic Lifecycles | Expiration & Revocation | Security records (`RefreshToken`, `EmailVerification`) possess purpose-built lifecycle mechanisms (`IsRevoked`, `ExpiresAt`, `VerifiedAt`). Adding `IsDeleted` would be redundant and weaken security state machines. |
| **F. Join / Relationship Data** | Relational Junctions | Direct Link Management | Junction links (`UserRole`, `ProblemTechnologies`) represent existence of relationships. Unlinking a role simply removes the junction row or tracks unassignment without soft-delete bloat. |

---

## 5. Global Query Filter Strategy

DevPilot AI implements centralized EF Core Global Query Filters in `DevPilotDbContext.OnModelCreating`:

```csharp
foreach (var entityType in modelBuilder.Model.GetEntityTypes())
{
    if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
    {
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var falseConstant = Expression.Constant(false);
        var equalExpression = Expression.Equal(property, falseConstant);
        var lambda = Expression.Lambda(equalExpression, parameter);
        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
    }
}
```

### Key Guarantees:
1. **Automatic Filtering**: All LINQ queries against soft-deletable entities automatically prepend `WHERE [e].[IsDeleted] = 0` (or `false`). Developers never need to write manual `.Where(x => !x.IsDeleted)` clauses.
2. **Navigation Properties**: Related collections loaded via `.Include()` and `.ThenInclude()` automatically respect query filters, ensuring soft-deleted entities are not inadvertently leaked through navigation trees.
3. **Explicit Opt-Out**: Only authorized system routines (such as duplicate email detection on registration or administrative audit tools) can bypass filters using `.IgnoreQueryFilters()`.

---

## 6. User Account Deletion Behavior (`DELETE /api/users/me`)

DevPilot AI provides a dedicated account deletion endpoint:

```
DELETE /api/users/me
Authorization: Bearer <access_token>
```

### Security & Business Invariants:
1. **Authenticated Context Only**: The endpoint requires a valid JWT bearer token.
2. **Authoritative Identity**: The `userId` is strictly extracted from `ClaimTypes.NameIdentifier` on the authenticated `ClaimsPrincipal`. The endpoint never accepts a `userId` in the route or request body.
3. **Zero Cross-User Deletion**: A user can only delete their own account.
4. **Permanent Data Preservation**: The `User` database row is never physically deleted. `IsDeleted` is set to `true` and `DeletedAt` is stamped with UTC time.
5. **Idempotency**: Repeated deletion calls are safe and produce no redundant state mutations.
6. **Historical Data Isolation**: All past `Problems`, `CodeReviews`, `ChallengeAttempts`, `ProblemSolutions`, and `AIRequests` created by the user remain fully intact in the database for platform integrity and audit compliance.

---

## 7. Security and Session Handling on User Soft Delete

When a user account is soft-deleted, DevPilot AI enforces comprehensive session termination and access denial:

1. **Refresh Token Invalidation**:
   All active refresh tokens for the user (`!rt.IsRevoked`) are immediately marked `IsRevoked = true` and stamped with `RevokedAt = DateTime.UtcNow`.
2. **Immediate JWT Invalidation**:
   In `Program.cs`, the JWT Bearer authentication pipeline attaches to `JwtBearerEvents.OnTokenValidated`:
   ```csharp
   options.Events = new JwtBearerEvents
   {
       OnTokenValidated = async context =>
       {
           var dbContext = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
           var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (int.TryParse(userIdClaim, out var userId))
           {
               var userExists = await dbContext.Users.AnyAsync(u => u.UserId == userId);
               if (!userExists)
               {
                   context.Fail("User account is inactive or deleted.");
               }
           }
           else
           {
               context.Fail("Invalid user identification in token.");
           }
       }
   };
   ```
   Because `dbContext.Users` applies the global query filter `!u.IsDeleted`, `userExists` evaluates to `false` for soft-deleted accounts. Consequently, **even existing, unexpired JWTs are immediately rejected with HTTP 401 Unauthorized** on subsequent requests.
3. **Login Denial**:
   `AuthService.LoginAsync` fails to find the soft-deleted user (or detects `user.IsDeleted`) and returns a generic `401 Unauthorized` ("Invalid email or password.") without disclosing account existence or deletion status.
4. **Token Refresh Denial**:
   `AuthService.RefreshTokenAsync` rejects requests because all tokens are revoked and the user is null/inactive.
5. **Password Recovery Denial**:
   `AuthService.ForgotPasswordAsync` silently skips email dispatch for deleted accounts while returning the standard generic message, preventing user enumeration.
6. **Verification Denial**:
   `AuthService.VerifyEmailAsync` and `AuthService.ResendVerificationAsync` reject operations for deleted accounts.

---

## 8. Unique Constraints & Identifier Reservation Policy

### `Users.Email`:
- The unique constraint `HasIndex(u => u.Email).IsUnique()` remains a **strict, non-filtered unique index** in SQL Server.
- Soft-deleted users continue to reserve their email address.
- **Rationale**:
  1. Prevents account hijacking and identity spoofing (an attacker cannot register a new account with the email of a deleted user to inherit their public reputation or historical problem solutions).
  2. Preserves relational integrity with external identity systems and communication history.
  3. In `RegisterAsync`, registration attempts using a soft-deleted email are rejected via `IgnoreQueryFilters()` with `"An account with this email address already exists."`

### `Problems.ProblemCode`:
- Unique constraint `HasIndex(p => p.ProblemCode).IsUnique()` remains a strict unique index.
- Problem codes (e.g. `DEV-000042`) are permanent business references and must never be recycled.

---

## 9. Physical Delete Audit & Hard-Delete Exceptions

A full audit of the codebase confirmed:
- **Zero calls** to `DbSet.Remove()`, `RemoveRange()`, `ExecuteDelete()`, or raw SQL `DELETE` in production application code.
- Direct physical removal is strictly prohibited for domain business entities.
- Relational tests in `DatabaseRelationshipTests` explicitly verify that attempting physical deletion of a parent entity with children is blocked by `DeleteBehavior.NoAction` and throws an `InvalidOperationException`.

---

## 10. Strict Non-Cascade Policy

DevPilot AI strictly forbids cascade deletion in the database:
- `foreignKey.DeleteBehavior = DeleteBehavior.NoAction;` is configured for every foreign key relationship across the entire EF Core data model in `DevPilotDbContext.OnModelCreating`.
- No `ON DELETE CASCADE` constraints exist anywhere in the SQL Server schema.
- Parent and child lifecycles are decoupled: deleting a parent entity logically hides the parent via query filters while child historical records (`DebuggingAttempts`, `ProblemRequests`, `Feedbacks`, `Solutions`) remain securely preserved in the database.
