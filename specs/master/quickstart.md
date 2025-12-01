# Identity Tenant Management - Developer Quickstart

**Version**: 1.0.0
**Last Updated**: 2025-11-30
**Target Audience**: New developers joining the project

This guide will get you up and running with the IdentityTenantManagement platform in under 30 minutes.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Clone and Restore](#clone-and-restore)
3. [Keycloak Setup](#keycloak-setup)
4. [Database Configuration](#database-configuration)
5. [Run Migrations](#run-migrations)
6. [Start the Services](#start-the-services)
7. [Test Onboarding Workflow](#test-onboarding-workflow)
8. [Run Tests](#run-tests)
9. [Debugging Tips](#debugging-tips)
10. [Common Issues](#common-issues)

---

## Prerequisites

Before you begin, ensure you have the following installed:

### Required Software

| Software | Version | Download Link | Notes |
|----------|---------|---------------|-------|
| **.NET SDK** | 9.0.203+ | https://dotnet.microsoft.com/download | Required for all projects |
| **SQL Server** | 2019+ or SQL Express | https://www.microsoft.com/sql-server | Local database for tenant/user data |
| **Keycloak** | 25.0+ | https://www.keycloak.org/downloads | Identity provider (runs on port 8080) |
| **Git** | Latest | https://git-scm.com/downloads | Source control |

### Optional Tools

- **Visual Studio 2022** or **JetBrains Rider** - Recommended IDEs
- **SQL Server Management Studio (SSMS)** - For database inspection
- **Postman** or **Bruno** - For API testing

### Verify Installation

```bash
# Check .NET version
dotnet --version
# Expected output: 9.0.203 or higher

# Check SQL Server (Windows)
sqlcmd -S localhost\SQLEXPRESS -Q "SELECT @@VERSION"

# Check Git
git --version
```

---

## Clone and Restore

### 1. Clone the Repository

```bash
git clone <repository-url>
cd IdentityTenantManagement
```

### 2. Restore NuGet Packages

```bash
# Restore all projects in the solution
dotnet restore IdentityTenantManagement.sln
```

### 3. Verify Project Structure

```
IdentityTenantManagement/
├── IdentityTenantManagement/              # Core API (ASP.NET Core 9.0)
├── IdentityTenantManagement.BlazorApp/    # Demo web app (Blazor Server)
├── IdentityTenantManagementDatabase/      # EF Core database project
├── KeycloakAdapter/                       # Keycloak integration adapter
├── IdentityTenantManagement.Tests/        # Unit/integration tests
└── specs/                                 # Documentation and contracts
```

---

## Keycloak Setup

Keycloak is the identity provider used for authentication and organization management.

### Option A: Docker (Recommended)

```bash
# Pull Keycloak image
docker pull quay.io/keycloak/keycloak:25.0

# Run Keycloak on port 8080
docker run -d \
  --name keycloak-dev \
  -p 8080:8080 \
  -e KEYCLOAK_ADMIN=admin \
  -e KEYCLOAK_ADMIN_PASSWORD=admin \
  quay.io/keycloak/keycloak:25.0 \
  start-dev
```

### Option B: Manual Installation

1. Download Keycloak from https://www.keycloak.org/downloads
2. Extract to a directory (e.g., `C:\keycloak`)
3. Start Keycloak:
   ```bash
   cd C:\keycloak\bin
   kc.bat start-dev
   ```

### Configure Keycloak Realm

1. Open Keycloak Admin Console: http://localhost:8080
2. Login with username: `admin`, password: `admin`
3. Create a new realm named **`Organisations`**
4. Create a client with ID **`IdentityTenantManagement`**:
   - Client type: **OpenID Connect**
   - Client authentication: **ON** (confidential)
   - Standard flow: **Enabled**
   - Direct access grants: **Enabled**
5. Navigate to **Credentials** tab and copy the **Client Secret**
6. Update `appsettings.json` with the client secret (see next section)

**Note**: The application's seed data will create default roles and permissions automatically.

---

## Database Configuration

### 1. Create SQL Server Database

The application expects a SQL Server database. The migrations will create the schema automatically.

**Option A: SQL Express (Recommended for Development)**

Connection string already configured in `appsettings.json`:
```
Server=localhost\SQLEXPRESS;Database=IdentityTenantManagement;Trusted_Connection=True;Encrypt=False;
```

**Option B: SQL Server LocalDB**

Update `appsettings.json` connection string:
```json
"ConnectionStrings": {
  "OnboardingDatabase": "Server=(localdb)\\mssqllocaldb;Database=IdentityTenantManagement;Trusted_Connection=True;Encrypt=False;"
}
```

**Option C: Full SQL Server Instance**

Update `appsettings.json` with your connection details:
```json
"ConnectionStrings": {
  "OnboardingDatabase": "Server=localhost;Database=IdentityTenantManagement;User Id=sa;Password=YourPassword;Encrypt=False;"
}
```

### 2. Configure appsettings.json

The `IdentityTenantManagement/appsettings.json` file contains configuration templates:

```json
{
  "KeycloakConfig": {
    "BaseUrl": "http://localhost:8080",
    "Realm": "Organisations",
    "ClientId": "IdentityTenantManagement",
    "ClientSecret": "", // SECURITY: Configure via User Secrets
    "TokenEndpoint": "/realms/{realm}/protocol/openid-connect/token"
  },
  "ConnectionStrings": {
    "OnboardingDatabase": "Server=localhost\\SQLEXPRESS;Database=IdentityTenantManagement;Trusted_Connection=True;Encrypt=False;"
  }
}
```

**⚠️ REQUIRED: Configure Keycloak Client Secret via User Secrets**

For security, the ClientSecret is NOT stored in appsettings.json. You MUST configure it using User Secrets:

```bash
cd IdentityTenantManagement
dotnet user-secrets set "KeycloakConfig:ClientSecret" "YOUR_CLIENT_SECRET_FROM_KEYCLOAK"
```

To verify your user secret was set:

```bash
dotnet user-secrets list
```

---

## Run Migrations

The application uses Entity Framework Core Code-First migrations to manage the database schema.

### Apply Migrations

```bash
# Navigate to the main API project
cd IdentityTenantManagement

# Apply all migrations to the database
dotnet ef database update --project ../IdentityTenantManagementDatabase

# Verify migration success
# You should see output like: "Done. Applied X migrations."
```

### Expected Database Schema

The migrations will create **18 tables**:

**Core Entities**:
- `Tenants` - Organization/tenant records
- `Users` - Global user accounts
- `TenantUsers` - User-tenant memberships
- `UserProfiles` - User profile data
- `TenantUserProfiles` - Tenant-specific profiles
- `TenantDomains` - Verified domains for tenants

**Permission System**:
- `Roles` - Predefined roles (org-admin, org-manager, org-user)
- `Permissions` - Granular permissions (invite-users, delete-users, etc.)
- `PermissionGroups` - Permission categories
- `RolePermissions` - Role-permission mappings
- `TenantUserRoles` - User role assignments
- `UserPermissions` - Direct permission grants
- `UserStatusTypes` - Status lookup (active, inactive, suspended)

**Infrastructure**:
- `IdentityProviders` - Keycloak configuration
- `ExternalIdentities` - ID mapping (internal ↔ Keycloak)
- `ExternalIdentityEntityTypes` - Entity type lookup
- `GlobalSettings` - Key-value configuration
- `RegistrationFailureLogs` - Saga failure audit trail

### Seed Data

The migrations automatically seed:
- **3 Roles**: org-admin, org-manager, org-user
- **6 Permissions**: invite-users, view-users, edit-users, delete-users, view-tenant-settings, edit-tenant-settings
- **1 Permission Group**: System Administration
- **Role-Permission mappings** (org-admin has all permissions)
- **4 User Status Types**: active, inactive, suspended, pending
- **1 Identity Provider**: Keycloak (http://localhost:8080) ⚠️

**⚠️ Important**: The seed data hardcodes `BaseUrl: "http://localhost:8080/"`. Update the `IdentityProviders` table if your Keycloak runs on a different URL.

---

## Start the Services

### 1. Start the Core API

```bash
cd IdentityTenantManagement
dotnet run
```

**Expected Output**:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
      Now listening on: http://localhost:5000
```

**Test API Health**:
```bash
curl http://localhost:5000/api/v1/Tenants/Create -X POST \
  -H "Content-Type: application/json" \
  -d '{"name":"TestOrg","domain":"test-org"}'
```

### 2. Start the Blazor App (Optional)

In a **new terminal**:

```bash
cd IdentityTenantManagement.BlazorApp
dotnet run
```

**Expected Output**:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5XXX
```

Open your browser to the displayed URL to access the onboarding form.

---

## Test Onboarding Workflow

The onboarding workflow creates a new tenant and its initial administrator user in a single atomic operation (using saga pattern).

### Using the Blazor App

1. Navigate to http://localhost:5XXX (see Blazor App output)
2. Click **"Onboarding"** in the navigation menu
3. Fill in the form:
   - **Organization Name**: Acme Corporation
   - **Domain**: acme-corp
   - **First Name**: John
   - **Last Name**: Doe
   - **Username**: admin
   - **Email**: john.doe@acme.com
   - **Password**: SecureP@ssw0rd!
4. Click **"Complete Onboarding"**
5. Upon success, you'll see a confirmation message

### Using cURL (API Direct)

```bash
curl -X POST http://localhost:5000/api/v1/Onboarding/OnboardOrganisation \
  -H "Content-Type: application/json" \
  -d '{
    "createTenantModel": {
      "name": "AcmeCorporation",
      "domain": "acme-corp"
    },
    "createUserModel": {
      "userName": "admin",
      "firstName": "John",
      "lastName": "Doe",
      "email": "john.doe@acme.com",
      "password": "SecureP@ssw0rd!"
    }
  }'
```

**Expected Response**:
```json
{
  "message": "Client Onboarded successfully"
}
```

### Verify in Keycloak

1. Open Keycloak Admin Console: http://localhost:8080
2. Switch to **Organisations** realm
3. Navigate to **Organizations**
4. You should see **acme-corp** organization
5. Click on it and verify the user **admin** is a member

### Verify in Database

```sql
-- Check tenant was created
SELECT * FROM Tenants WHERE Name = 'Acme Corporation';

-- Check user was created
SELECT * FROM Users WHERE Email = 'john.doe@acme.com';

-- Check tenant-user association
SELECT * FROM TenantUsers;

-- Check external identity mapping
SELECT * FROM ExternalIdentities;
```

---

## Run Tests

The project includes unit tests using xUnit and Moq.

### Run All Tests

```bash
# From solution root
dotnet test IdentityTenantManagement.Tests/IdentityTenantManagement.Tests.csproj

# With detailed output
dotnet test IdentityTenantManagement.Tests/IdentityTenantManagement.Tests.csproj --verbosity normal
```

### Expected Output

```
Test run for IdentityTenantManagement.Tests.dll (.NETCoreApp,Version=v9.0)
Test Run Successful.
Total tests: X
     Passed: X
```

### Test Coverage Areas

Current tests cover:
- **Saga pattern**: OnboardingService rollback scenarios
- **Repository pattern**: CRUD operations with tenant isolation
- **Unit of Work**: Transaction management
- **Service layer**: Business logic validation

**Test Coverage**: ~55% (Target: 80% per constitution)

**Missing Tests** (see `research.md` for details):
- 6 tenant isolation tests
- 6 KeycloakAdapter contract tests
- 5 saga integration tests

---

## Debugging Tips

### Visual Studio / Rider

1. Set **IdentityTenantManagement** as the startup project
2. Set breakpoints in controllers or services
3. Press **F5** to start debugging

### Inspect HTTP Traffic

Use the built-in logging to see HTTP requests:

Edit `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information",  // Change from Warning
      "Microsoft.AspNetCore.HttpLogging": "Information"
    }
  }
}
```

### Database Debugging

Connect to SQL Server and inspect tables:
```sql
-- View all tenants
SELECT * FROM Tenants;

-- View all users with their tenant memberships
SELECT u.Email, t.Name AS TenantName, tu.JoinedAt
FROM Users u
JOIN TenantUsers tu ON u.Id = tu.UserId
JOIN Tenants t ON tu.TenantId = t.Id;

-- View user permissions
SELECT u.Email, p.Code AS Permission, tur.RoleId
FROM Users u
JOIN TenantUsers tu ON u.Id = tu.UserId
JOIN TenantUserRoles tur ON tu.Id = tur.TenantUserId
JOIN RolePermissions rp ON tur.RoleId = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id;
```

### Keycloak Debugging

Enable Keycloak event logging:
1. Keycloak Admin Console → **Realm Settings** → **Events**
2. Enable **Save Events**
3. Add event types: LOGIN, REGISTER, etc.
4. View events under **Events** tab

### Check Application Logs

Logs are written to console by default. Look for:
- **Saga rollback messages**: Indicates compensating transactions
- **RegistrationFailureLog entries**: Persisted saga failures
- **401/403 responses**: Authentication/authorization issues

---

## Common Issues

### Issue: "Cannot connect to SQL Server"

**Symptoms**:
```
Microsoft.Data.SqlClient.SqlException: A network-related or instance-specific error occurred
```

**Solutions**:
1. Verify SQL Server is running:
   ```bash
   # Windows
   sc query MSSQL$SQLEXPRESS
   ```
2. Check connection string in `appsettings.json`
3. Ensure SQL Server Browser service is running (for named instances)
4. Try using `Server=localhost,1433` instead of `localhost\SQLEXPRESS`

---

### Issue: "Keycloak authentication failed"

**Symptoms**:
```json
{
  "success": false,
  "errorMessage": "Invalid credentials"
}
```

**Solutions**:
1. Verify Keycloak is running: http://localhost:8080
2. Check realm name is **Organisations** (case-sensitive)
3. Verify client ID is **IdentityTenantManagement**
4. Ensure client secret in `appsettings.json` matches Keycloak
5. Check organization domain exists in Keycloak

---

### Issue: "Migration already applied"

**Symptoms**:
```
The migration 'XXXXXX_MigrationName' has already been applied
```

**Solutions**:
This is normal if migrations were already applied. To reset:

```bash
# Drop and recreate database
dotnet ef database drop --project IdentityTenantManagementDatabase
dotnet ef database update --project IdentityTenantManagementDatabase
```

**⚠️ Warning**: This deletes all data. Only do this in development.

---

### Issue: "CORS errors in Blazor App"

**Symptoms**:
```
Access to fetch at 'http://localhost:5000/api/...' from origin 'https://localhost:5XXX' has been blocked by CORS policy
```

**Solutions**:
1. Check `Program.cs` in the API project has CORS configured
2. The current CORS policy allows all origins (`AllowAnyOrigin()`) - this should work
3. If issues persist, add specific origin:
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddDefaultPolicy(policy =>
       {
           policy.WithOrigins("https://localhost:5XXX")
                 .AllowAnyHeader()
                 .AllowAnyMethod();
       });
   });
   ```

---

### Issue: "Stack trace exposed in error response"

**Symptoms**:
API returns full stack traces in production-like environments.

**Root Cause**:
This is a known security issue documented in `research.md` (OWASP audit finding #12).

**Location**: `UsersController.cs:118`

**Temporary Workaround**:
Set `ASPNETCORE_ENVIRONMENT=Production` to suppress stack traces (but this hides other useful development info).

**Permanent Fix**:
Will be addressed in Phase 4 (security hardening tasks).

---

### Issue: "Missing authentication middleware"

**Symptoms**:
Authorization attributes don't work, all users can access protected endpoints.

**Root Cause**:
Critical security issue - `app.UseAuthentication()` is missing from `Program.cs:99`.

**Fix** (documented in `research.md`, not yet applied):
```csharp
// In Program.cs, BEFORE app.UseAuthorization()
app.UseAuthentication();  // ← ADD THIS LINE
app.UseAuthorization();
```

**⚠️ Note**: This fix is scheduled for Phase 3 (API versioning implementation).

---

## Next Steps

Once you have the platform running:

1. **Read the Documentation**:
   - `specs/master/data-model.md` - Entity relationships and database schema
   - `specs/master/research.md` - Security audit, saga pattern, API versioning
   - `specs/master/contracts/` - OpenAPI specifications for all endpoints
   - `.specify/memory/constitution.md` - Platform principles and non-negotiables

2. **Review Security Findings**:
   - See `research.md` Section 2: OWASP Top 10 Compliance Audit
   - **CRITICAL**: 90% of endpoints lack authorization
   - **CRITICAL**: Missing authentication middleware
   - 12 security issues documented with code fixes

3. **Explore the Codebase**:
   - `IdentityTenantManagement/Services/OnboardingService.cs` - Saga pattern implementation
   - `IdentityTenantManagementDatabase/Repositories/` - Repository pattern with tenant isolation
   - `KeycloakAdapter/Services/` - Keycloak integration abstraction

4. **Run Pending Tasks**:
   - See `specs/master/tasks.md` for 111 tasks across 5 phases
   - Current progress: 51/111 (46% complete)
   - Next: Phase 2 (Test Coverage Expansion) - 17 missing tests

5. **Contribute**:
   - Follow the repository pattern for data access (see constitution Principle VI)
   - Maintain strict tenant isolation (see constitution Principle I - NON-NEGOTIABLE)
   - Add tests for new features (constitution Principle IV - NON-NEGOTIABLE)

---

## Getting Help

- **Project Documentation**: See `specs/master/` directory
- **Architecture Decisions**: See `.specify/memory/constitution.md`
- **API Contracts**: See `specs/master/contracts/` (OpenAPI YAML files)
- **Issue Tracker**: [Link to issue tracker if available]
- **Team Chat**: [Link to Slack/Teams if available]

---

**Document Version**: 1.0.0
**Last Updated**: 2025-11-30
**Maintained By**: Platform Team