# Platform Architecture Research

**Date**: 2025-11-30
**Purpose**: Document architectural decisions, security audit findings, and future architecture plans for IdentityTenantManagement platform baseline

---

## 1. Saga Pattern for Distributed Transactions

### Overview

The platform uses the **Saga pattern** to coordinate distributed transactions between Keycloak (external identity provider) and the local SQL Server database. Since Keycloak's API doesn't support distributed two-phase commit protocols, the saga ensures eventual consistency through compensating transactions.

### Implementation Location

**Primary Implementation**: `IdentityTenantManagement/Services/OnboardingService.cs`
- Coordinates tenant and user onboarding across Keycloak and local database
- Implements forward transactions (create operations) and compensating transactions (delete operations)

**Secondary Implementation**: `IdentityTenantManagement/Services/RegistrationProcessorService.cs`
- Background service (`BackgroundService`) that runs every 1 minute
- Processes Keycloak registration events asynchronously
- Implements compensating rollback when database sync fails

### Saga Workflow - OnboardingService.OnboardOrganisationAsync

**Forward Transaction Steps** (Happy Path):

1. **Create User in Keycloak**
   - Call `IKCUserService.CreateUserAsync(model.CreateUserModel)`
   - Retrieve created user: `GetUserByEmailAsync(email)` → obtains Keycloak User ID

2. **Create Organization (Realm) in Keycloak**
   - Call `IKCOrganisationService.CreateOrgAsync(model.CreateTenantModel)`
   - Retrieve created org: `GetOrganisationByDomain(domain)` → obtains Keycloak Org ID

3. **Link User to Organization in Keycloak**
   - Call `IKCOrganisationService.AddUserToOrganisationAsync(UserTenantModel)`

4. **Begin Local Database Transaction**
   - `unitOfWork.BeginTransactionAsync()`

5. **Create Local Database Entities**
   - Create `User` entity (internal GUID, NOT Keycloak GUID)
   - Create `Tenant` entity (internal GUID, NOT Keycloak GUID)
   - Create `ExternalIdentity` records mapping internal GUIDs to Keycloak UUIDs for both User and Tenant
   - Create `TenantUser` relationship with org-admin `TenantUserRole`
   - Create `UserProfile` with user's name from Keycloak
   - Create `TenantUserProfile` linking profile to tenant-user relationship

6. **Commit Local Database Transaction**
   - `unitOfWork.CommitAsync()`

### Compensating Transactions (Rollback Strategy)

The saga implements **reverse-order compensating transactions** when failures occur:

**Scenario 1: Organization Creation Fails** (after user created)
```csharp
try {
    CreateUser();
    CreateOrganization(); // FAILS HERE
} catch {
    DeleteUser(keycloakUserId); // Compensating transaction
    throw;
}
```

**Scenario 2: User-to-Organization Link Fails** (after both created)
```csharp
try {
    CreateUser();
    CreateOrganization();
    AddUserToOrganisation(); // FAILS HERE
} catch {
    DeleteOrganisation(keycloakOrgId); // Compensate step 2
    DeleteUser(keycloakUserId);         // Compensate step 1
    throw;
}
```

**Scenario 3: Database Commit Fails** (after Keycloak operations succeed)
```csharp
try {
    CreateUser();
    CreateOrganization();
    AddUserToOrganisation();
    unitOfWork.BeginTransactionAsync();
    // Add entities to database...
    unitOfWork.CommitAsync(); // FAILS HERE
} catch {
    unitOfWork.RollbackAsync();                      // Rollback DB transaction
    RemoveUserFromOrganisationAsync(userId, orgId);  // Unlink in Keycloak
    DeleteOrganisationAsync(orgId);                  // Delete org in Keycloak
    DeleteUserAsync(userId);                         // Delete user in Keycloak
    throw;
}
```

### Idempotency Guarantees

**Question**: Are compensating transactions idempotent (safe to retry)?

**Analysis**:

1. **Keycloak Delete Operations**:
   - `DeleteUserAsync(userId)`: Keycloak API returns `204 No Content` if user exists and is deleted
   - **NOT strictly idempotent**: If user already deleted, Keycloak returns `404 Not Found`
   - **Mitigation**: OnboardingService catches exceptions and logs them, but doesn't fail the compensating transaction chain

2. **Database Rollback**:
   - `unitOfWork.RollbackAsync()`: Entity Framework rollback is idempotent (no-op if transaction already aborted)

3. **Retry Safety**:
   - Compensating transactions should handle `404 Not Found` gracefully (user/org already deleted is acceptable)
   - Tests verify: `OnboardOrganisationAsync_ContinuesCompensatingTransactions_EvenWhenSomeFail` (OnboardingServiceTests.cs:637)
   - Even if `RemoveUserFromOrganisationAsync` fails, the saga continues to delete the organization and user

**Recommendation**:
- Add explicit `404` handling in KeycloakAdapter methods to make delete operations idempotent
- Consider adding retry logic with exponential backoff for transient Keycloak failures

### Saga Coordination - RegistrationProcessorService

**Purpose**: Handle asynchronous user registrations that occur directly in Keycloak (not through OnboardingService)

**Workflow**:
1. Background service runs every 1 minute
2. Calls `IKCEventsService.GetRecentRegistrationEventsAsync()` to fetch Keycloak registration events
3. For each registration:
   - Calls `IUserService.AddInvitedUserToDatabaseAsync()` to sync user to local database
   - **On Failure**: Attempts compensating rollback by calling `IKCUserService.DeleteUserAsync(userId)`
   - Logs failure to `RegistrationFailureLog` table with `KeycloakUserRolledBack` flag

**Key Characteristics**:
- **Eventually consistent**: 1-minute delay between Keycloak registration and database sync
- **Compensating rollback**: If database sync fails, deletes Keycloak user
- **Audit trail**: Logs all failures to `RegistrationFailureLog` for manual investigation

**Idempotency Concern**:
- Service runs every minute and queries events from "last 70 minutes"
- **Risk**: Same registration could be processed multiple times
- **Mitigation Needed**: Check if user already exists in database before processing (`AddInvitedUserToDatabaseAsync` should be idempotent or check for duplicates)

### Test Coverage

**Comprehensive saga tests exist in** `IdentityTenantManagement.Tests/Services/OnboardingServiceTests.cs`:

- `OnboardOrganisationAsync_DeletesCreatedUser_WhenOrganisationCreationFails` (line 513)
- `OnboardOrganisationAsync_DeletesUserAndOrg_WhenLinkingFails` (line 542)
- `OnboardOrganisationAsync_RollsBackAllChanges_WhenDatabaseCommitFails` (line 585)
- `OnboardOrganisationAsync_ContinuesCompensatingTransactions_EvenWhenSomeFail` (line 637)

**Missing Tests**:
- No tests for RegistrationProcessorService saga logic
- No tests for duplicate registration event handling
- No tests for Keycloak `404` handling in compensating transactions

### Recommendations

1. **Add explicit idempotency to KeycloakAdapter delete methods**:
   ```csharp
   public async Task DeleteUserAsync(string userId) {
       try {
           await _httpClient.DeleteAsync($"/users/{userId}");
       } catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
           _logger.LogWarning("User {UserId} already deleted", userId);
           // Idempotent - already deleted is success
       }
   }
   ```

2. **Add duplicate detection to RegistrationProcessorService**:
   - Check if `ExternalIdentity` with `ExternalIdentifier == keycloakUserId` already exists
   - Skip processing if user already synced

3. **Consider adding distributed tracing/correlation IDs**:
   - Track saga execution across Keycloak API calls and database operations
   - Use `ILogger` scopes with correlation IDs for debugging

4. **Document saga timeout/retry policy**:
   - How long should compensating transactions retry on transient failures?
   - Should Keycloak operations have circuit breakers?

---

## 2. Security Compliance Audit (OWASP Top 10)

**Audit Date**: 2025-11-30
**Scope**: All controllers, authorization middleware, exception handler, CORS configuration

### Critical Findings (MUST FIX)

#### 🔴 CRITICAL #1: Missing Authentication Middleware

**Location**: `Program.cs:99`

**Issue**: `app.UseAuthorization()` is called but `app.UseAuthentication()` is **never configured**.

```csharp
// Line 99 - Authorization without Authentication!
app.UseAuthorization();
app.MapControllers();
```

**Impact**: Authorization checks (including `RequirePermissionAttribute`) cannot work without authentication. User claims (`user_id`, `tenant_id`) will never be populated, causing all protected endpoints to return 401 Unauthorized even with valid credentials.

**Recommendation**:
```csharp
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors("AllowBlazorApp");
app.UseAuthentication(); // ADD THIS
app.UseAuthorization();
app.MapControllers();
```

**OWASP Category**: A07:2021 - Identification and Authentication Failures

---

#### 🔴 CRITICAL #2: Broken Access Control - Unauthorized Endpoints

**Affected Controllers**: OnboardingController, TenantsController, UsersController, AuthenticationController

**Issue**: Most endpoints have **NO authorization checks** whatsoever. Anyone with network access can:

1. **OnboardingController** (`api/Onboarding/OnboardOrganisation`):
   - ❌ NO authorization attribute
   - Create unlimited tenants and admin users
   - **Risk**: Resource exhaustion, malicious tenant creation

2. **TenantsController**:
   - ❌ ALL endpoints lack authorization (`Create`, `GetTenantByDomain`, `InviteExistingUser`, `InviteUser`)
   - Anyone can create tenants, invite users to ANY organization
   - **Risk**: Complete bypass of tenant isolation

3. **UsersController**:
   - ❌ `GetOrganizationUsers(organizationId)` - No validation on organizationId parameter
   - Anyone can query users from **any organization** by guessing/brute-forcing organization IDs
   - ❌ `CreateUser`, `GetUserByEmail`, `UpdateUser` - No authorization checks
   - ✅ `DeleteUser` - ONLY endpoint with `[RequirePermission("delete-users")]`
   - ❌ `GetRecentRegistrations` - Exposes registration events to anyone

4. **AuthenticationController**:
   - ❌ `GetUserPermissions(keycloakUserId, keycloakOrgId)` - No authorization
   - Anyone can query **any user's permissions** by knowing their IDs
   - **Risk**: Information disclosure, privilege escalation reconnaissance

**Recommendation**: Add authorization to ALL endpoints:
- Onboarding: Consider requiring admin token or public rate-limited endpoint with captcha
- Tenants: Require authentication + tenant context validation
- Users: Require authentication + validate requesting user has tenant context matching requested resource
- Permissions endpoint: Require authentication + validate requesting user matches queried user

**OWASP Category**: A01:2021 - Broken Access Control

---

#### 🔴 CRITICAL #3: Information Leakage via Stack Traces

**Location**: `UsersController.cs:118`

```csharp
catch (Exception ex)
{
    return StatusCode(500, new
    {
        message = "Failed to retrieve registration events",
        error = ex.Message,
        stackTrace = ex.StackTrace  // ⚠️ EXPOSES INTERNAL PATHS
    });
}
```

**Impact**: Stack traces expose:
- Internal file paths (`C:\Users\chris\SourceCode\...`)
- Method names and internal architecture
- Database connection details (if in connection string exceptions)
- Assists attackers in reconnaissance

**Recommendation**: Remove `stackTrace` from all responses. Use GlobalExceptionHandler for consistent error responses.

**OWASP Category**: A05:2021 - Security Misconfiguration

---

### High Severity Findings

#### 🟠 HIGH #1: Cross-Tenant Data Access Vulnerability

**Location**: `UsersController.cs:35-38`

```csharp
[HttpGet("organization/{organizationId}")]
public async Task<IActionResult> GetOrganizationUsers(string organizationId)
{
    var users = await _kcOrganisationService.GetOrganisationUsersAsync(organizationId);
    return Ok(users);
}
```

**Issue**:
- No validation that requesting user belongs to `organizationId`
- Accepts `organizationId` directly from URL without tenant context check
- **Violates Constitution Principle I: Multi-Tenant Isolation**

**Recommendation**:
```csharp
[HttpGet("organization/{organizationId}")]
public async Task<IActionResult> GetOrganizationUsers(string organizationId)
{
    var tenantId = User.FindFirst("tenant_id")?.Value;
    if (tenantId != organizationId)
    {
        return Forbid(); // User can only query their own organization
    }
    var users = await _kcOrganisationService.GetOrganisationUsersAsync(organizationId);
    return Ok(users);
}
```

**OWASP Category**: A01:2021 - Broken Access Control

---

#### 🟠 HIGH #2: Input Validation Bypass

**Location**: `TenantsController.cs:36`

```csharp
[HttpPost("GetTenantByDomain")]
public async Task<IActionResult> GetTenantByDomain([FromBody] string body)
{
    await _kcOrganisationService.GetOrganisationByDomain(body);
    return Ok(new {message="Organisation created successfully", tenantName=body });
}
```

**Issues**:
1. Accepts raw `string` instead of validated model
2. No length validation (could be 10MB string causing DoS)
3. No format validation (domain regex)
4. No sanitization for SQL injection (if used in raw queries downstream)

**Recommendation**:
```csharp
public class GetTenantByDomainRequest
{
    [Required]
    [StringLength(253, MinimumLength = 3)] // RFC 1035 max domain length
    [RegularExpression(@"^[a-zA-Z0-9-]+\.[a-zA-Z]{2,}$")]
    public string Domain { get; set; }
}

[HttpPost("GetTenantByDomain")]
public async Task<IActionResult> GetTenantByDomain([FromBody] GetTenantByDomainRequest request)
{
    // ModelState.IsValid check
    ...
}
```

**OWASP Category**: A03:2021 - Injection

---

#### 🟠 HIGH #3: Exception Message Exposure

**Location**: `UsersController.cs:95`

```csharp
catch (Exception ex)
{
    return StatusCode(500, new { message = "Failed to delete user", error = ex.Message });
}
```

**Issue**: `ex.Message` can contain sensitive information:
- Database constraint violations revealing schema
- Connection timeouts revealing infrastructure
- Keycloak API errors revealing external system details

**Recommendation**: Use GlobalExceptionHandler consistently. Let it map exceptions to sanitized responses.

**OWASP Category**: A05:2021 - Security Misconfiguration

---

### Medium Severity Findings

#### 🟡 MEDIUM #1: CORS Misconfiguration

**Location**: `Program.cs:32-40`

```csharp
options.AddPolicy("AllowBlazorApp", policy =>
{
    policy.WithOrigins("https://localhost:5280", "http://localhost:5104")
          .AllowAnyHeader()
          .AllowAnyMethod();
});
```

**Issues**:
1. Hardcoded localhost URLs (not configurable for production)
2. Includes insecure HTTP origin (`http://localhost:5104`)
3. `.AllowAnyHeader()` and `.AllowAnyMethod()` are overly permissive

**Recommendation**:
```csharp
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("CORS origins not configured");

options.AddPolicy("AllowBlazorApp", policy =>
{
    policy.WithOrigins(allowedOrigins)
          .WithHeaders("Content-Type", "Authorization")
          .WithMethods("GET", "POST", "PUT", "DELETE");
});
```

**OWASP Category**: A05:2021 - Security Misconfiguration

---

#### 🟡 MEDIUM #2: Insufficient Rate Limiting

**Location**: `Program.cs:22-28`

**Current State**: Only `/login` endpoint has rate limiting (5 requests/min)

**Issue**: Other sensitive endpoints are NOT rate-limited:
- Onboarding (tenant creation) - unlimited resource creation
- User creation - brute-force user enumeration
- Password reset (if implemented) - account takeover attempts

**Recommendation**: Add rate limiting policies:
```csharp
options.AddFixedWindowLimiter("onboarding", options =>
{
    options.PermitLimit = 10;
    options.Window = TimeSpan.FromHours(1);
});

options.AddSlidingWindowLimiter("api", options =>
{
    options.PermitLimit = 100;
    options.Window = TimeSpan.FromMinutes(1);
    options.SegmentsPerWindow = 4;
});
```

Then apply to controllers:
```csharp
[EnableRateLimiting("onboarding")]
public class OnboardingController : ControllerBase { ... }
```

**OWASP Category**: A07:2021 - Identification and Authentication Failures

---

#### 🟡 MEDIUM #3: Sensitive Data in Logs

**Location**: `RegistrationFailureLog.cs:6-14`

**Issue**: Stores full exception details (`ErrorDetails`) in database, which may include:
- Stack traces with internal paths
- Connection strings (if exception during DB connection)
- Keycloak API responses with sensitive tokens

**Current Implementation**:
```csharp
public string ErrorDetails { get; set; } = string.Empty; // Stores ex.ToString()
```

**Recommendation**:
1. Sanitize `ErrorDetails` before storing (remove stack traces, redact connection strings)
2. Store only `ex.Message` in production (stack traces only in dev)
3. Add data retention policy (auto-delete logs > 90 days)

**OWASP Category**: A09:2021 - Security Logging and Monitoring Failures

---

### Low Severity / Best Practices

#### 🟢 LOW #1: Missing ModelState Validation

**Locations**: Most POST endpoints

**Issue**: Only `AuthenticationController.Login` validates `ModelState.IsValid` (line 31-34). Other endpoints trust model binding without validation.

**Recommendation**: Add global model validation filter:
```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ModelStateValidationFilter>();
});
```

---

#### 🟢 LOW #2: Not Implemented Endpoint Exposed

**Location**: `TenantsController.cs:50-54`

```csharp
[HttpPost("AddDomainToOrganisation")]
public async Task<IActionResult> AddDomainToOrganisation([FromBody] TenantDomainModel body)
{
    throw new NotImplementedException();
}
```

**Issue**: Endpoint is publicly exposed in Swagger and returns 500 error.

**Recommendation**: Remove or add `[ApiExplorerSettings(IgnoreApi = true)]` until implemented.

---

### Positive Security Controls ✅

**What's Working Well**:

1. **GlobalExceptionHandler** (Middleware/GlobalExceptionHandler.cs):
   - ✅ Sanitizes generic exceptions to "An unexpected error occurred"
   - ✅ Provides TraceId for support correlation without leaking internals
   - ✅ Uses RFC 7231 error types (standardized)

2. **Security Headers** (Program.cs:79-94):
   - ✅ `X-Content-Type-Options: nosniff` (prevents MIME sniffing)
   - ✅ `X-Frame-Options: DENY` (prevents clickjacking)
   - ✅ `X-XSS-Protection: 1; mode=block`
   - ✅ `Referrer-Policy: strict-origin-when-cross-origin`
   - ✅ `Permissions-Policy` (disables geolocation, microphone, camera)
   - ✅ `Strict-Transport-Security` (HSTS in production only)

3. **RequirePermissionAttribute** (Authorization/RequirePermissionAttribute.cs):
   - ✅ Validates user_id and tenant_id claims
   - ✅ Validates GUID format
   - ✅ Supports "require all" vs "require any" permissions
   - ✅ Returns proper 401/403 status codes

4. **Rate Limiting** (Program.cs:17-29):
   - ✅ Login endpoint protected (5 requests/min)
   - ✅ No queueing (immediate rejection)

5. **Privacy in Logging** (AuthenticationController.cs:37):
   - ✅ Logs organization but NOT username

---

### Compliance Summary

| OWASP Top 10 Category | Status | Issues Found |
|-----------------------|--------|--------------|
| A01: Broken Access Control | ❌ FAIL | Missing authorization on 90% of endpoints, cross-tenant access |
| A02: Cryptographic Failures | ✅ PASS | Passwords delegated to Keycloak, HTTPS enforced |
| A03: Injection | ⚠️ PARTIAL | Raw string inputs, no SQL injection tests performed |
| A04: Insecure Design | ⚠️ PARTIAL | Saga pattern good, but missing auth design |
| A05: Security Misconfiguration | ❌ FAIL | Missing authentication middleware, stack trace leaks, CORS hardcoded |
| A06: Vulnerable Components | ✅ PASS | Using .NET 9.0, EF Core 9.0.9 (latest) |
| A07: Identification/Authentication | ❌ FAIL | Missing authentication middleware (critical) |
| A08: Software/Data Integrity | ⚠️ PARTIAL | No code signing, no SRI for CDN resources |
| A09: Logging/Monitoring Failures | ⚠️ PARTIAL | Good logging, but stores sensitive data in RegistrationFailureLog |
| A10: Server-Side Request Forgery | ✅ PASS | No SSRF vectors identified |

**Overall Assessment**: ❌ **NOT PRODUCTION READY**

**Blockers**:
1. Missing `app.UseAuthentication()` (critical)
2. Missing authorization on 90% of endpoints (critical)
3. Cross-tenant data access vulnerabilities (high)

---

### Action Items (Priority Order)

**Must Fix Before Production**:
1. ✅ Add `app.UseAuthentication()` to Program.cs (1 line fix)
2. ✅ Add authentication requirement to ALL controllers (use `[Authorize]` attribute)
3. ✅ Validate tenant context in all multi-tenant endpoints
4. ✅ Remove stack trace from UsersController error responses
5. ✅ Fix CORS configuration to use appsettings.json

**Should Fix Before v1.0**:
6. ⚠️ Add rate limiting to onboarding and user creation endpoints
7. ⚠️ Replace raw string inputs with validated models
8. ⚠️ Sanitize RegistrationFailureLog.ErrorDetails
9. ⚠️ Remove or hide NotImplemented endpoint

**Nice to Have**:
10. 🟢 Add global ModelState validation filter
11. 🟢 Add CSP (Content Security Policy) header
12. 🟢 Implement request/response logging middleware

---

## 3. API Versioning Implementation Plan

**Research Date**: 2025-11-30

### ASP.NET Core API Versioning Options

**Recommended Package**: `Asp.Versioning.Mvc` version 9.0+ (compatible with .NET 9)

**Versioning Strategies Available**:

1. **URL Path Versioning** (RECOMMENDED for this project)
   ```
   GET /api/v1/Onboarding/OnboardOrganisation
   GET /api/v2/Onboarding/OnboardOrganisation
   ```
   - ✅ Most explicit and discoverable
   - ✅ Easy to test (different URLs)
   - ✅ Works well with API gateways and caching
   - ✅ RESTful and widely adopted
   - ❌ Requires route updates

2. **Query String Versioning**
   ```
   GET /api/Onboarding/OnboardOrganisation?api-version=1.0
   GET /api/Onboarding/OnboardOrganisation?api-version=2.0
   ```
   - ✅ No route changes needed
   - ❌ Less discoverable (hidden in query params)
   - ❌ Can be lost in URL copying

3. **Header Versioning**
   ```
   GET /api/Onboarding/OnboardOrganisation
   Header: api-version: 1.0
   ```
   - ✅ Clean URLs
   - ❌ Not visible in browser/Swagger
   - ❌ Harder to test manually

4. **Media Type Versioning** (Accept header)
   ```
   GET /api/Onboarding/OnboardOrganisation
   Header: Accept: application/vnd.identitytenantmanagement.v1+json
   ```
   - ✅ True REST hypermedia approach
   - ❌ Most complex to implement
   - ❌ Not well-supported by .NET versioning library

**Decision**: Use **URL Path Versioning** for clarity and discoverability.

---

### Implementation Steps

#### 1. Install NuGet Package
```bash
dotnet add package Asp.Versioning.Mvc --version 9.0.0
```

#### 2. Configure in Program.cs
```csharp
// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true; // Add api-supported-versions header
    options.ApiVersionReader = new UrlSegmentApiVersionReader(); // URL path versioning
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV"; // Format: v1, v2
    options.SubstituteApiVersionInUrl = true;
});
```

#### 3. Update Swagger Configuration
```csharp
builder.Services.AddSwaggerGen(options =>
{
    var provider = builder.Services.BuildServiceProvider()
        .GetRequiredService<IApiVersionDescriptionProvider>();

    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerDoc(description.GroupName, new OpenApiInfo
        {
            Title = $"IdentityTenantManagement API {description.ApiVersion}",
            Version = description.ApiVersion.ToString(),
            Description = "Multi-tenant B2B SaaS identity management platform"
        });
    }
});

// Update SwaggerUI to show version selector
app.UseSwaggerUI(options =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerEndpoint(
            $"/swagger/{description.GroupName}/swagger.json",
            description.GroupName.ToUpperInvariant());
    }
});
```

#### 4. Update Controllers
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class OnboardingController : ControllerBase
{
    // Endpoints automatically use /api/v1/Onboarding
}
```

#### 5. Mark Deprecated Endpoints
```csharp
[ApiVersion("1.0", Deprecated = true)]
[Route("api/v{version:apiVersion}/[controller]")]
public class OldOnboardingController : ControllerBase
{
    // Returns deprecation warning in api-deprecated-versions header
}
```

---

### Deprecation Policy

**Timeline for Breaking Changes**:

1. **Deprecation Announcement** (Version N):
   - Mark endpoint with `[ApiVersion("N", Deprecated = true)]`
   - Add deprecation date to API response headers: `Sunset: Sat, 01 Jan 2026 00:00:00 GMT`
   - Update Swagger documentation with deprecation notice
   - Send email notification to API consumers

2. **Warning Period** (Minimum 6 months):
   - Continue serving deprecated endpoints with warnings
   - Log usage of deprecated endpoints for monitoring
   - Provide migration guide in documentation

3. **Removal** (Version N+2):
   - Remove deprecated endpoints
   - Return `410 Gone` for requests to removed versions
   - Update Swagger to remove old version documentation

**Example Deprecation Flow**:
```
v1.0 Released: 2025-01-01
v2.0 Released: 2025-07-01 (v1.0 marked deprecated, sunset date: 2026-01-01)
v1.0 Removed: 2026-01-01 (6 months after deprecation)
```

**Breaking Change Definition**:
- Removing an endpoint
- Changing required request fields
- Changing response schema in incompatible way
- Changing HTTP status codes
- Changing authentication/authorization requirements

**Non-Breaking Changes** (can be added to existing version):
- Adding optional request fields
- Adding new response fields
- Adding new endpoints
- Improving error messages
- Performance improvements

---

### Versioning for Existing Controllers

**Current Controllers** → **v1 Routes**:

| Controller | Current Route | v1 Route |
|------------|--------------|----------|
| OnboardingController | `/api/Onboarding` | `/api/v1/Onboarding` |
| TenantsController | `/api/Tenants` | `/api/v1/Tenants` |
| UsersController | `/api/Users` | `/api/v1/Users` |
| AuthenticationController | `/api/Authentication` | `/api/v1/Authentication` |

**Migration Plan**:
1. Add versioning infrastructure to Program.cs
2. Update all controllers to use `[ApiVersion("1.0")]` and versioned routes
3. Update BlazorApp OnboardingApiClient to use `/api/v1/` URLs
4. Update integration tests to use versioned endpoints
5. Deprecate old non-versioned routes (or redirect to v1)

---

### Version Negotiation

**Supported Scenarios**:

1. **Client specifies version**: `/api/v1/Onboarding` → Uses v1
2. **Client requests non-existent version**: `/api/v99/Onboarding` → 400 Bad Request with supported versions
3. **Client uses default version**: If `AssumeDefaultVersionWhenUnspecified = true`, uses `DefaultApiVersion`

**Response Headers**:
```
api-supported-versions: 1.0, 2.0
api-deprecated-versions: 1.0
```

---

### Testing Strategy

**Version-Specific Tests**:
```csharp
[Test]
public async Task OnboardOrganisation_V1_ReturnsExpectedSchema()
{
    var response = await _client.PostAsync("/api/v1/Onboarding/OnboardOrganisation", content);
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    // Assert v1 schema
}

[Test]
public async Task OnboardOrganisation_V2_ReturnsEnhancedSchema()
{
    var response = await _client.PostAsync("/api/v2/Onboarding/OnboardOrganisation", content);
    // Assert v2 schema includes new fields
}
```

---

### Documentation Requirements

**API-VERSIONING.md** (to be created in specs/master/):
- Versioning policy overview
- Deprecation timeline and process
- How to request specific versions
- Migration guides for major version upgrades
- Breaking vs non-breaking change definitions
- Current supported versions table

---

### Future Considerations

1. **Version Sunset Automation**: Background job to automatically return `410 Gone` for sunset versions
2. **Version Analytics**: Track which API versions are being used (for deprecation planning)
3. **Automated Migration Guides**: Generate migration docs from OpenAPI schema diffs
4. **Version-Specific Rate Limits**: Apply different rate limits to deprecated versions

---

## 4. Test Coverage Roadmap

**Planning Date**: 2025-11-30

### Current Test Coverage

**Existing Test Files** (from code inspection):
- `OnboardingServiceTests.cs` - Comprehensive saga tests (11+ test methods)
- Unit tests for services with Moq mocking

**Coverage Gaps Identified**:

1. **Tenant Isolation** (Constitutional Requirement)
   - No tests verify cross-tenant data access prevention
   - Repository methods not tested for tenant filtering
   - API endpoints not tested for tenant context validation

2. **KeycloakAdapter Contract Tests** (Constitutional Requirement)
   - No contract tests for Keycloak API adapter
   - Operations not verified: CreateRealm, CreateUser, DeleteRealm, AssignRole
   - Mocking strategy undefined (should use Moq for HTTP client)

3. **Saga Integration Tests** (Constitutional Requirement)
   - Existing OnboardingService tests cover saga logic well
   - Missing: RegistrationProcessorService saga tests
   - Missing: Duplicate event handling tests
   - Missing: Keycloak 404 idempotency tests

---

### Test Scenarios by Category

#### Tenant Isolation Tests (`IdentityTenantManagement.Tests/Integration/TenantIsolationTests.cs`)

**Purpose**: Verify Multi-Tenant Isolation (Constitution Principle I)

**Test 1**: Repository-level isolation
```csharp
[Test]
public async Task GetByTenantIdAsync_OnlyReturnsUsersFromSpecifiedTenant()
{
    // Arrange: Create Tenant A with User A, Tenant B with User B
    // Act: Query UserRepository.GetByTenantIdAsync(tenantAId)
    // Assert: Only User A returned, not User B
}
```

**Test 2**: Cross-tenant TenantUser query
```csharp
[Test]
public async Task TenantUserRepository_CannotAccessOtherTenantUsers()
{
    // Arrange: Authenticate as Tenant A admin
    // Act: Attempt GetByTenantIdAsync(tenantBId)
    // Assert: Returns empty or throws UnauthorizedAccessException
}
```

**Test 3**: Role repository tenant scoping
```csharp
[Test]
public async Task RoleRepository_OnlyReturnsRolesScopedToTenant()
{
    // Arrange: Create tenant-specific roles
    // Act: Query roles for Tenant A
    // Assert: Only Tenant A roles returned
}
```

**Test 4**: Permission repository boundaries
```csharp
[Test]
public async Task PermissionRepository_EnforcesTenantBoundaries()
{
    // Arrange: Permissions assigned to Tenant A user
    // Act: Query permissions for Tenant B context
    // Assert: No Tenant A permissions returned
}
```

**Test 5**: End-to-end API test
```csharp
[Test]
public async Task UsersController_GetOrganizationUsers_ReturnsForbiddenForWrongTenant()
{
    // Arrange: Authenticate as Tenant A user, request Tenant B users
    // Act: GET /api/Users/organization/{tenantBId}
    // Assert: 403 Forbidden or 404 Not Found
}
```

**Test 6**: Tenant context validation
```csharp
[Test]
public async Task UpdateUser_ValidatesTenantContextMatchesUserId()
{
    // Arrange: Tenant A admin attempts to update Tenant B user
    // Act: PUT /api/Users/{tenantBUserId}
    // Assert: 403 Forbidden
}
```

---

#### KeycloakAdapter Contract Tests (`IdentityTenantManagement.Tests/Contract/KeycloakAdapterContractTests.cs`)

**Purpose**: Verify Keycloak API adapter contracts without external dependencies

**Mocking Strategy**: Use Moq to mock `HttpClient` or `IHttpClientFactory`

**Test 1**: CreateRealm contract
```csharp
[Test]
public async Task CreateRealmAsync_CallsCorrectKeycloakEndpoint()
{
    // Arrange: Mock HttpClient to expect POST to /admin/realms
    var mockHttp = new Mock<HttpMessageHandler>();
    mockHttp.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ...)
        .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Created })
        .Verifiable();

    // Act: await adapter.CreateRealmAsync(realmModel)
    // Assert: Verify POST to correct endpoint with correct payload
}
```

**Test 2**: CreateUser contract
```csharp
[Test]
public async Task CreateUserAsync_SendsCorrectUserPayload()
{
    // Mock: POST /admin/realms/{realm}/users
    // Assert: Payload includes email, firstName, lastName, enabled=true
}
```

**Test 3**: AssignRole contract
```csharp
[Test]
public async Task AssignRoleAsync_CallsRoleMappingEndpoint()
{
    // Mock: POST /admin/realms/{realm}/users/{userId}/role-mappings/realm
    // Assert: Role ID included in payload
}
```

**Test 4**: DeleteRealm compensating transaction
```csharp
[Test]
public async Task DeleteRealmAsync_HandlesNotFoundGracefully()
{
    // Mock: DELETE returns 404 Not Found
    // Assert: No exception thrown (idempotent delete)
}
```

**Test 5**: SyncUser operation
```csharp
[Test]
public async Task SyncUserAsync_UpdatesKeycloakUserAttributes()
{
    // Mock: PUT /admin/realms/{realm}/users/{userId}
    // Assert: Correct attributes updated
}
```

**Test 6**: Error handling
```csharp
[Test]
public async Task CreateUserAsync_ThrowsKeycloakExceptionOn409Conflict()
{
    // Mock: POST returns 409 Conflict (user already exists)
    // Assert: Throws KeycloakException with meaningful message
}
```

---

#### OnboardingService Saga Integration Tests (Additional Scenarios)

**Existing Coverage**: ✅ Well-tested in `OnboardingServiceTests.cs`
- Success path
- Organization creation failure rollback
- User-to-org link failure rollback
- Database commit failure rollback
- Partial compensation failure handling

**Additional Tests Needed**:

**Test 7**: RegistrationProcessorService saga
```csharp
[Test]
public async Task ProcessRegistrationsAsync_RollsBackKeycloakUserOnDatabaseFailure()
{
    // Arrange: Mock Keycloak events API, force database failure
    // Act: Background service processes event
    // Assert: Keycloak user deleted, RegistrationFailureLog created
}
```

**Test 8**: Duplicate event handling
```csharp
[Test]
public async Task ProcessRegistrationsAsync_SkipsDuplicateRegistrationEvents()
{
    // Arrange: Same registration event processed twice
    // Act: Second processing attempt
    // Assert: Skipped (user already exists in ExternalIdentity table)
}
```

**Test 9**: Keycloak 404 idempotency
```csharp
[Test]
public async Task DeleteUserAsync_TreatsNotFoundAsSuccess()
{
    // Arrange: Mock DELETE /users/{id} returns 404
    // Act: Compensating transaction attempts delete
    // Assert: No exception, saga continues
}
```

---

### Test Infrastructure Requirements

**In-Memory Database** (for integration tests):
```csharp
var options = new DbContextOptionsBuilder<IdentityTenantManagementContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;
```

**HTTP Client Mocking** (for Keycloak adapter):
```csharp
// Install package: Moq
var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
var httpClient = new HttpClient(mockHttpMessageHandler.Object);
```

**Test Data Builders** (for consistency):
```csharp
public class TenantBuilder
{
    public Tenant Build() => new Tenant { Id = Guid.NewGuid(), Name = "Test Tenant", ... };
}
```

---

### Testing Best Practices

1. **AAA Pattern**: Arrange, Act, Assert in every test
2. **One assertion per test**: Focus on single behavior
3. **Meaningful names**: `MethodName_Scenario_ExpectedBehavior`
4. **Deterministic GUIDs**: Use `Guid.Parse()` for predictable test data
5. **Cleanup**: Dispose DbContext and HttpClient in teardown
6. **Parallel execution**: Tests must not share state

---

### Test Coverage Goals

| Category | Target Coverage | Current | Gap |
|----------|----------------|---------|-----|
| Repository Layer | 80% | ~60% (estimated) | +20% |
| Service Layer | 90% | ~75% | +15% |
| API Controllers | 70% | ~30% | +40% |
| Keycloak Adapter | 80% | 0% | +80% |
| **Overall** | **80%** | **~55%** | **+25%** |

**Priority**: Constitutional requirements (tenant isolation, adapter contracts, saga integration) before coverage percentage targets.

---

## 5. Seed Data Implementation

**Review Date**: 2025-11-30

### Current Seed Data

**Location**: `IdentityTenantManagementDatabase/DbContexts/IdentityTenantManagementContext.cs:31-378`

**Method**: Entity Framework Core `.HasData()` in `OnModelCreating`

### Seeded Entities

#### 1. IdentityProvider (Lines 35-42)
```csharp
Id: 049284C1-FF29-4F28-869F-F64300B69719 (deterministic)
Name: "Keycloak"
ProviderType: "oidc"
BaseUrl: "http://localhost:8080/"
```

**Idempotency**: ✅ **PASS** - Deterministic GUID ensures INSERT only runs once

#### 2. ExternalIdentityEntityTypes (Lines 45-56)
```csharp
- user: 86E6890C-0B3F-4278-BD92-A4FD2EA55413
- tenant: F6D6E7FC-8998-4B77-AD31-552E5C76C3DD
```

**Idempotency**: ✅ **PASS** - Deterministic GUIDs

#### 3. UserStatusTypes (Lines 184-217)
```csharp
- active: 90A89389-7891-4784-90DF-F556E95BCCD9
- inactive: 7F313E08-F83E-43DF-B550-C11014592AB7
- suspended: 52EE270F-D6DE-4F96-A578-3C8E84AF4B9B
- pending: 3981B8F5-FFBD-426A-ABDE-A723631B3536
```

**Idempotency**: ✅ **PASS** - Deterministic GUIDs

#### 4. PermissionGroups (Lines 222-231)
```csharp
- SystemAdministration: 1A2B3C4D-5E6F-7890-ABCD-EF1234567890
```

**Idempotency**: ✅ **PASS**

#### 5. Roles (Lines 238-263)
```csharp
- org-admin: A1B2C3D4-E5F6-7890-ABCD-EF1234567890
- org-manager: B2C3D4E5-F678-90AB-CDEF-123456789ABC
- org-user: C3D4E5F6-7890-ABCD-EF12-3456789ABCDE
```

**Idempotency**: ✅ **PASS**

#### 6. Permissions (Lines 273-328)
```csharp
- invite-users: D4E5F678-90AB-CDEF-1234-56789ABCDEF0
- view-users: E5F67890-ABCD-EF12-3456-789ABCDEF012
- update-users: F6789ABC-DEF1-2345-6789-ABCDEF012345
- delete-users: 0789ABCD-EF12-3456-789A-BCDEF0123456
- assign-permissions: 189ABCDE-F123-4567-89AB-CDEF01234567
- update-org-settings: 29ABCDEF-0123-4567-89AB-CDEF12345678
```

**Idempotency**: ✅ **PASS**

#### 7. RolePermissions (Lines 332-346)
```csharp
org-admin: ALL 6 permissions
org-manager: invite-users, view-users, update-users
org-user: NO permissions
```

**Idempotency**: ✅ **PASS** - Each RolePermission has deterministic GUID

#### 8. GlobalSettings (Lines 349-358)
```csharp
- RequirePermissionToGrant: "true"
  (Users must have permission themselves before granting to others)
```

**Idempotency**: ✅ **PASS**

---

### Idempotency Guarantees

**How EF Core `.HasData()` Works**:
1. Generates migration with `INSERT` statements
2. Each migration runs once (tracked in `__EFMigrationsHistory` table)
3. Re-running `dotnet ef database update` with same migration is safe (no-op)
4. **Key Requirement**: Primary keys must be deterministic (not `Guid.NewGuid()`)

**Verdict**: ✅ **ALL SEED DATA IS IDEMPOTENT**

**Proof**:
- All entities use `Guid.Parse("...")` with hardcoded GUIDs
- No random GUID generation (`Guid.NewGuid()`) in seed data
- No date fields using `DateTime.Now` (all use fixed UTC dates)
- No database sequences or auto-increment fields in seed data

**Re-Running Migrations**: Safe to run `dotnet ef database update` multiple times:
1. First run: Seed data inserted
2. Subsequent runs: Migration already applied, skipped
3. New database: All migrations re-applied, seed data inserted once

---

### Seed Data Testing Recommendation

**Test**: Verify seed data integrity
```csharp
[Test]
public async Task SeedData_AllRolesHaveCorrectPermissions()
{
    // Arrange: Fresh in-memory database with migrations applied
    // Act: Query Roles with RolePermissions
    // Assert:
    //   - org-admin has 6 permissions
    //   - org-manager has 3 permissions
    //   - org-user has 0 permissions
}

[Test]
public async Task SeedData_UserStatusTypesExist()
{
    // Assert: 4 status types exist (active, inactive, suspended, pending)
}
```

---

### Potential Issues & Recommendations

**Issue 1**: Hardcoded Keycloak BaseUrl
- **Location**: Line 40: `BaseUrl = "http://localhost:8080/"`
- **Problem**: Not configurable for production (different Keycloak instances)
- **Recommendation**: Either:
  1. Remove IdentityProvider seed data (configure via appsettings.json instead)
  2. Add migration to UPDATE BaseUrl from configuration on startup

**Issue 2**: Hardcoded CreatedAt Dates
- **Impact**: Low (cosmetic only)
- **Current**: All seed data has `CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)`
- **Recommendation**: Keep as-is (deterministic for testing)

**Issue 3**: No Tenant Seed Data
- **Impact**: Developers must manually create first tenant via API
- **Recommendation**: Add optional development-only seed tenant:
  ```csharp
  if (environment.IsDevelopment())
  {
      modelBuilder.Entity<Tenant>().HasData(...);
  }
  ```

---

## 6. Payment and Reporting Future Architecture

**Planning Date**: 2025-11-30

### Payment Gateway Integration Patterns

**Recommended Gateway**: Stripe (industry standard for B2B SaaS)

**Alternative**: PayPal (enterprise invoicing capabilities)

#### Stripe Integration Architecture

**Model Structure**:
```
Tenant
  ├── StripeCustomer (1:1)
  │   ├── StripeCustomerId (string)
  │   ├── DefaultPaymentMethodId (string)
  │   └── BillingEmail (string)
  ├── Subscription (1:many - supports multiple products)
  │   ├── StripeSubscriptionId (string)
  │   ├── PlanTier (enum: Free, Pro, Enterprise)
  │   ├── Status (enum: Active, Canceled, PastDue, Unpaid)
  │   ├── CurrentPeriodStart (DateTime)
  │   ├── CurrentPeriodEnd (DateTime)
  │   └── CancelAtPeriodEnd (bool)
  └── Invoice (1:many - historical billing records)
      ├── StripeInvoiceId (string)
      ├── AmountDue (decimal)
      ├── AmountPaid (decimal)
      ├── Currency (string)
      ├── Status (enum: Draft, Open, Paid, Void, Uncollectible)
      └── InvoicePdf (string - URL to PDF)
```

**Stripe Events Webhook Handler**:
```csharp
[HttpPost("api/webhooks/stripe")]
public async Task<IActionResult> HandleStripeWebhook()
{
    // Verify webhook signature
    // Handle events:
    //   - invoice.paid → Update subscription status
    //   - invoice.payment_failed → Suspend tenant access
    //   - customer.subscription.deleted → Cancel subscription
}
```

**Payment Method Storage**: Stripe Checkout or Payment Intent API
- **DO NOT** store credit card details in local database (PCI DSS compliance nightmare)
- Store only Stripe PaymentMethod ID (tokenized reference)

---

### Product Tiering Model

**Recommended Structure**:

```csharp
public enum PlanTier
{
    Free = 0,
    Pro = 1,
    Enterprise = 2
}

public class ProductTier
{
    public Guid Id { get; set; }
    public PlanTier Tier { get; set; }
    public string Name { get; set; } // "Free", "Professional", "Enterprise"
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; } // Discounted
    public int MaxUsers { get; set; } // e.g., 5, 50, unlimited (-1)
    public int MaxOrganizations { get; set; } // For multi-org tenants
    public bool SsoEnabled { get; set; }
    public bool AdvancedPermissions { get; set; }
    public bool ApiAccessEnabled { get; set; }
    public int ApiRateLimit { get; set; } // requests/minute
}
```

**Feature Flags per Tier**:
```csharp
public class TenantFeatureFlags
{
    public Guid TenantId { get; set; }
    public PlanTier CurrentTier { get; set; }
    public bool SsoEnabled => CurrentTier >= PlanTier.Enterprise;
    public bool AdvancedReporting => CurrentTier >= PlanTier.Pro;
    public int MaxUsers => CurrentTier switch {
        PlanTier.Free => 5,
        PlanTier.Pro => 50,
        PlanTier.Enterprise => int.MaxValue,
        _ => 5
    };
}
```

**Enforcement**: Middleware checks feature flags before allowing access:
```csharp
[HttpPost("api/Tenants/EnableSSO")]
[RequireFeature(Feature.Sso)] // Custom attribute
public async Task<IActionResult> EnableSSO()
{
    // Only accessible if tenant's plan includes SSO
}
```

---

### Financial Reporting Requirements

**Revenue Tracking**:
- Monthly Recurring Revenue (MRR) per tenant
- Annual Recurring Revenue (ARR) calculation
- Churn rate (canceled subscriptions / total subscriptions)
- Expansion revenue (upgrades from Free → Pro → Enterprise)

**Database Model**:
```csharp
public class RevenueSnapshot
{
    public Guid Id { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal MonthlyRecurringRevenue { get; set; }
    public decimal AnnualRecurringRevenue { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int FreeT ierCount { get; set; }
    public int ProTierCount { get; set; }
    public int EnterpriseTierCount { get; set; }
    public decimal ChurnRate { get; set; } // Percentage
}
```

**Background Job** (runs monthly):
```csharp
public class RevenueCalculationService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CalculateMonthlyRevenue();
            await Task.Delay(TimeSpan.FromDays(30), stoppingToken);
        }
    }
}
```

---

### Usage Metrics Tracking

**Metrics to Track**:
- Active users per tenant (daily/monthly)
- API calls per tenant (rate limiting enforcement)
- Storage used per tenant (if file uploads supported)
- Feature usage (which features are used most)

**Telemetry Model**:
```csharp
public class UsageMetric
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime Date { get; set; }
    public string MetricType { get; set; } // "active_users", "api_calls", "storage_mb"
    public decimal Value { get; set; }
}
```

**Aggregation for Billing**:
```csharp
// Charge for overage (e.g., > 50 users on Pro plan)
var activeUsers = await _usageMetrics
    .Where(m => m.TenantId == tenantId && m.MetricType == "active_users" && m.Date >= billingPeriodStart)
    .AverageAsync(m => m.Value);

if (activeUsers > tier.MaxUsers)
{
    var overageCharge = (activeUsers - tier.MaxUsers) * overagePricePerUser;
    // Create Stripe invoice line item for overage
}
```

---

### Invoicing Architecture

**Invoice Generation** (automated):
1. Stripe creates invoice automatically for subscriptions
2. Webhook receives `invoice.created` event
3. Store invoice record in local database for reporting
4. Email invoice PDF to tenant billing contact

**Manual Invoicing** (for enterprise custom contracts):
```csharp
public class ManualInvoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public List<InvoiceLineItem> LineItems { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } // "Draft", "Sent", "Paid", "Overdue"
}
```

---

### Implementation Roadmap

**Phase 1: Product Tiers** (2-3 weeks)
- Define ProductTier entity and seed data
- Add CurrentTier to Tenant model
- Implement feature flag middleware
- Enforce limits (max users, API rate limits)

**Phase 2: Stripe Integration** (3-4 weeks)
- Install Stripe .NET SDK
- Implement StripeCustomer, Subscription, Invoice models
- Build webhook handler for payment events
- Create Stripe Checkout session for plan upgrades
- Test payment flows in Stripe test mode

**Phase 3: Usage Tracking** (2 weeks)
- Implement UsageMetric model
- Add telemetry middleware (API call tracking)
- Background job to calculate daily active users
- Dashboard showing usage per tenant

**Phase 4: Financial Reporting** (2 weeks)
- RevenueSnapshot model and calculation service
- Admin dashboard for MRR/ARR charts
- Churn rate calculation
- Export to CSV/Excel for accounting

**Phase 5: Invoicing** (1 week)
- Store Stripe invoices in local database
- Email invoice PDFs to billing contacts
- Manual invoice generation for enterprise

**Total Estimated Effort**: 10-12 weeks

---

### Third-Party Dependencies

**Required Packages**:
- `Stripe.net` - Official Stripe SDK
- `Hangfire` (optional) - Background job scheduling for revenue calculations
- `ClosedXML` (optional) - Excel export for financial reports

**External Services**:
- Stripe account (production + test mode)
- Email service (SendGrid, AWS SES) for invoice delivery
- PDF generation (if custom invoices needed, otherwise use Stripe PDFs)
