# Data Model Documentation

**Project**: IdentityTenantManagement
**Database**: SQL Server via Entity Framework Core 9.0.9
**Approach**: Code-First with Migrations
**Last Updated**: 2025-11-30

---

## Overview

The IdentityTenantManagement database schema supports multi-tenant B2B SaaS identity management with strict tenant isolation, flexible permission systems, and external identity provider integration (currently Keycloak, extensible to Auth0, Azure AD, etc.).

**Entity Count**: 18 entities
**Tenant-Scoped Entities**: 8 entities enforce tenant boundaries
**Global Entities**: 10 entities shared across system

---

## Core Entities

### 1. Tenant (Organization)

**Purpose**: Represents a B2B customer organization

```csharp
public class Tenant
{
    public Guid Id { get; set; }                    // PK, internal tenant identifier
    public string Name { get; set; }                // Required, tenant display name
    public string Status { get; set; }              // "active", "suspended", "deleted"
    public DateTime CreatedAt { get; set; }         // UTC timestamp

    // Navigations
    public ICollection<User> Users { get; set; }
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; }
    public ICollection<TenantDomain> Domains { get; set; }
}
```

**Constraints**:
- `Name`: Required, max 200 chars (inferred from default string)
- `Status`: Default "active", enum recommended (active, suspended, deleted)

**Relationships**:
- **1:Many** → TenantDomain (domains)
- **1:Many** → ExternalIdentity (maps to Keycloak realm ID)
- **Many:Many** → User (via TenantUser)

**Indexes**:
- Recommended: `CREATE INDEX IX_Tenant_Status ON Tenant(Status)` for filtering active tenants
- Recommended: `CREATE INDEX IX_Tenant_CreatedAt ON Tenant(CreatedAt DESC)` for recent tenant queries

**Tenant Isolation**: Root tenant entity - all tenant-scoped queries start here

---

### 2. User (Global Identity)

**Purpose**: Global user identity that can belong to multiple tenants

```csharp
public class User
{
    public Guid Id { get; set; }                    // PK, internal user identifier
    public string Email { get; set; }               // Required, unique email
    public DateTime CreatedAt { get; set; }         // UTC timestamp

    // Navigations
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; }
}
```

**Constraints**:
- `Email`: Required, must be unique globally (recommend unique index)
- Email format validation should be enforced at application layer

**Relationships**:
- **1:Many** → ExternalIdentity (maps to Keycloak user ID)
- **Many:Many** → Tenant (via TenantUser)
- **1:Many** → UserProfile (NOT enforced, but typically 1:1)

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_User_Email ON User(Email)` - enforces email uniqueness
- Recommended: `CREATE INDEX IX_User_CreatedAt ON User(CreatedAt DESC)`

**Tenant Isolation**: Global entity - no tenant filtering

**Key Design Decision**: Users are global to support cross-tenant membership (B2B users may belong to multiple organizations)

---

### 3. TenantUser (Membership Relationship)

**Purpose**: Links users to tenants (many-to-many with metadata)

```csharp
public class TenantUser
{
    public Guid Id { get; set; }                    // PK
    public Guid TenantId { get; set; }              // FK → Tenant
    public Guid UserId { get; set; }                // FK → User
    public DateTime JoinedAt { get; set; }          // Membership timestamp

    // Navigations
    public Tenant Tenant { get; set; }
    public User User { get; set; }
    public ICollection<TenantUserRole> TenantUserRoles { get; set; }
}
```

**Constraints**:
- **UNIQUE CONSTRAINT**: (TenantId, UserId) - user can only join tenant once (enforced in DbContext configuration)

**Relationships**:
- **Many:1** → Tenant
- **Many:1** → User
- **1:Many** → TenantUserRole (user's roles within this tenant)
- **1:1** → TenantUserProfile (via TenantUserProfile.TenantUserId, unique constraint)

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_TenantUser_TenantId_UserId ON TenantUser(TenantId, UserId)` - enforced by EF Core
- Recommended: `CREATE INDEX IX_TenantUser_TenantId ON TenantUser(TenantId)` - for tenant member lookups
- Recommended: `CREATE INDEX IX_TenantUser_UserId ON TenantUser(UserId)` - for user's tenant membership queries

**Tenant Isolation**: ✅ TENANT-SCOPED - All queries MUST filter by TenantId

**Delete Behavior**: Cascade delete when Tenant or User deleted (configuration: DeleteBehavior.NoAction to prevent cycles)

---

### 4. TenantDomain (Domain Verification)

**Purpose**: Email domains owned by tenants (for SSO and user auto-assignment)

```csharp
public class TenantDomain
{
    public Guid Id { get; set; }                    // PK
    public Guid TenantId { get; set; }              // FK → Tenant
    public string Domain { get; set; }              // e.g., "example.com"
    public bool IsPrimary { get; set; }             // Default false
    public bool IsVerified { get; set; }            // Default false, DNS/email verification
    public DateTime CreatedAt { get; set; }

    // Navigations
    public Tenant Tenant { get; set; }
}
```

**Constraints**:
- `Domain`: Required, **UNIQUE globally** (one domain can only belong to one tenant)
- Domain format validation: regex `^[a-zA-Z0-9-]+\.[a-zA-Z]{2,}$`
- Max length: 253 characters (RFC 1035)

**Relationships**:
- **Many:1** → Tenant (DeleteBehavior.Cascade)

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_TenantDomain_Domain ON TenantDomain(Domain)` - enforced by EF Core
- Recommended: `CREATE INDEX IX_TenantDomain_TenantId_IsPrimary ON TenantDomain(TenantId, IsPrimary)` - for primary domain lookups

**Tenant Isolation**: ✅ TENANT-SCOPED

**Use Cases**:
1. Domain verification: User registers with `user@example.com`, if `example.com` is verified, auto-assign to owning tenant
2. SSO configuration: Primary domain used for SSO realm mapping

---

### 5. UserProfile (User Metadata)

**Purpose**: User's name and status (separate from authentication identity)

```csharp
public class UserProfile
{
    public Guid Id { get; set; }                    // PK
    public string FirstName { get; set; }           // Required
    public string LastName { get; set; }            // Required
    public Guid StatusId { get; set; }              // FK → UserStatusType
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigations
    public UserStatusType Status { get; set; }
    public ICollection<TenantUserProfile> TenantUserProfiles { get; set; }
}
```

**Constraints**:
- `FirstName`, `LastName`: Required, max 100 chars each (recommended)
- `StatusId`: Foreign key to UserStatusType (active, inactive, suspended, pending)

**Relationships**:
- **Many:1** → UserStatusType (DeleteBehavior.Restrict - cannot delete status type in use)
- **1:Many** → TenantUserProfile

**Indexes**:
- Recommended: `CREATE INDEX IX_UserProfile_StatusId ON UserProfile(StatusId)`
- Recommended: `CREATE INDEX IX_UserProfile_LastName_FirstName ON UserProfile(LastName, FirstName)` - for name searches

**Tenant Isolation**: Global entity (can be shared across tenant memberships via TenantUserProfile)

**Key Design Decision**: UserProfile is global to allow consistent identity across tenants

---

### 6. TenantUserProfile (Profile-Membership Link)

**Purpose**: Links UserProfile to TenantUser membership

```csharp
public class TenantUserProfile
{
    public Guid Id { get; set; }                    // PK
    public Guid TenantUserId { get; set; }          // FK → TenantUser (UNIQUE)
    public Guid UserProfileId { get; set; }         // FK → UserProfile
    public DateTime CreatedAt { get; set; }

    // Navigations
    public TenantUser TenantUser { get; set; }
    public UserProfile UserProfile { get; set; }
}
```

**Constraints**:
- **UNIQUE CONSTRAINT**: `TenantUserId` - each tenant membership has exactly one profile (enforced by EF Core)

**Relationships**:
- **1:1** → TenantUser (DeleteBehavior.Cascade)
- **Many:1** → UserProfile (DeleteBehavior.Restrict - profile can be reused)

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_TenantUserProfile_TenantUserId ON TenantUserProfile(TenantUserId)` - enforced by EF Core
- Recommended: `CREATE INDEX IX_TenantUserProfile_UserProfileId ON TenantUserProfile(UserProfileId)`

**Tenant Isolation**: ✅ TENANT-SCOPED (via TenantUser.TenantId)

---

## Permission System Entities

### 7. Role (Role Definition)

**Purpose**: Named role templates (org-admin, org-manager, org-user)

```csharp
public class Role
{
    public Guid Id { get; set; }                    // PK
    public string Name { get; set; }                // e.g., "org-admin" (kebab-case)
    public string DisplayName { get; set; }         // e.g., "Organization Administrator"
    public string Description { get; set; }         // Role description
    public DateTime CreatedAt { get; set; }

    // Navigations
    public ICollection<RolePermission> RolePermissions { get; set; }
    public ICollection<TenantUserRole> TenantUserRoles { get; set; }
}
```

**Constraints**:
- `Name`: Unique recommended, max 50 chars
- `DisplayName`: Max 100 chars
- `Description`: Max 500 chars

**Relationships**:
- **1:Many** → RolePermission (permissions included in role)
- **1:Many** → TenantUserRole (role assignments)

**Indexes**:
- Recommended: `CREATE UNIQUE INDEX IX_Role_Name ON Role(Name)`

**Tenant Isolation**: Global entity (roles are system-wide templates)

**Seeded Roles**:
1. `org-admin` - Full organization access (6 permissions)
2. `org-manager` - User management (3 permissions: invite, view, update users)
3. `org-user` - Standard member (0 permissions)

---

### 8. Permission (Granular Capability)

**Purpose**: Atomic permission (invite-users, delete-users, update-org-settings)

```csharp
public class Permission
{
    public Guid Id { get; set; }                    // PK
    public string Name { get; set; }                // e.g., "invite-users" (kebab-case)
    public string DisplayName { get; set; }         // e.g., "Invite Users"
    public string Description { get; set; }         // Permission description
    public Guid PermissionGroupId { get; set; }     // FK → PermissionGroup
    public DateTime CreatedAt { get; set; }

    // Navigations
    public PermissionGroup PermissionGroup { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; }
}
```

**Constraints**:
- `Name`: Unique recommended, max 50 chars
- Convention: Use kebab-case verbs (invite-users, view-reports, update-settings)

**Relationships**:
- **Many:1** → PermissionGroup (DeleteBehavior.Restrict)
- **1:Many** → RolePermission

**Indexes**:
- Recommended: `CREATE UNIQUE INDEX IX_Permission_Name ON Permission(Name)`
- Recommended: `CREATE INDEX IX_Permission_PermissionGroupId ON Permission(PermissionGroupId)`

**Tenant Isolation**: Global entity (permissions are system-wide capabilities)

**Seeded Permissions**:
1. `invite-users` - Invite new users to organization
2. `view-users` - View organization users
3. `update-users` - Update user information
4. `delete-users` - Remove users from organization
5. `assign-permissions` - Assign roles and permissions
6. `update-org-settings` - Modify organization settings

---

### 9. PermissionGroup (Permission Categorization)

**Purpose**: Groups related permissions for UI organization

```csharp
public class PermissionGroup
{
    public Guid Id { get; set; }                    // PK
    public string Name { get; set; }                // e.g., "SystemAdministration"
    public string DisplayName { get; set; }         // e.g., "System Administration"
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigations
    public ICollection<Permission> Permissions { get; set; }
}
```

**Constraints**:
- `Name`: Unique, PascalCase, max 50 chars

**Relationships**:
- **1:Many** → Permission

**Indexes**:
- Recommended: `CREATE UNIQUE INDEX IX_PermissionGroup_Name ON PermissionGroup(Name)`

**Tenant Isolation**: Global entity

**Seeded Groups**:
1. `SystemAdministration` - User and permission management

---

### 10. RolePermission (Role-Permission Mapping)

**Purpose**: Many-to-many relationship between Role and Permission

```csharp
public class RolePermission
{
    public Guid Id { get; set; }                    // PK
    public Guid RoleId { get; set; }                // FK → Role
    public Guid PermissionId { get; set; }          // FK → Permission
    public DateTime CreatedAt { get; set; }

    // Navigations
    public Role Role { get; set; }
    public Permission Permission { get; set; }
}
```

**Constraints**:
- **UNIQUE CONSTRAINT**: (RoleId, PermissionId) - cannot assign same permission to role twice

**Relationships**:
- **Many:1** → Role (DeleteBehavior.Cascade)
- **Many:1** → Permission (DeleteBehavior.Cascade)

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_RolePermission_RoleId_PermissionId ON RolePermission(RoleId, PermissionId)` - enforced by EF Core
- Recommended: `CREATE INDEX IX_RolePermission_PermissionId ON RolePermission(PermissionId)` - for reverse lookups

**Tenant Isolation**: Global entity (role templates are system-wide)

---

### 11. TenantUserRole (Role Assignment)

**Purpose**: Assigns roles to tenant users

```csharp
public class TenantUserRole
{
    public Guid Id { get; set; }                    // PK
    public Guid TenantUserId { get; set; }          // FK → TenantUser
    public Guid RoleId { get; set; }                // FK → Role
    public DateTime AssignedAt { get; set; }

    // Navigations
    public TenantUser TenantUser { get; set; }
    public Role Role { get; set; }
}
```

**Constraints**:
- **UNIQUE CONSTRAINT**: (TenantUserId, RoleId) - cannot assign same role twice

**Relationships**:
- **Many:1** → TenantUser (DeleteBehavior.Cascade)
- **Many:1** → Role (DeleteBehavior.Restrict - cannot delete role in use)

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_TenantUserRole_TenantUserId_RoleId ON TenantUserRole(TenantUserId, RoleId)` - enforced by EF Core
- Recommended: `CREATE INDEX IX_TenantUserRole_TenantUserId ON TenantUserRole(TenantUserId)` - for user's roles lookup

**Tenant Isolation**: ✅ TENANT-SCOPED (via TenantUser.TenantId)

---

### 12. UserPermission (Direct Permission Grant)

**Purpose**: Grants individual permissions to users (overrides role permissions)

```csharp
public class UserPermission
{
    public Guid Id { get; set; }                    // PK
    public Guid TenantUserId { get; set; }          // FK → TenantUser
    public Guid PermissionId { get; set; }          // FK → Permission
    public Guid? GrantedByUserId { get; set; }      // FK → User (audit trail)
    public DateTime CreatedAt { get; set; }

    // Navigations
    public TenantUser TenantUser { get; set; }
    public Permission Permission { get; set; }
    public User? GrantedByUser { get; set; }
}
```

**Constraints**:
- **UNIQUE CONSTRAINT**: (TenantUserId, PermissionId) - cannot grant same permission twice

**Relationships**:
- **Many:1** → TenantUser (DeleteBehavior.Cascade)
- **Many:1** → Permission (DeleteBehavior.Cascade)
- **Many:1** → User (GrantedByUser) (DeleteBehavior.Restrict - preserve audit trail)

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_UserPermission_TenantUserId_PermissionId ON UserPermission(TenantUserId, PermissionId)` - enforced by EF Core
- Recommended: `CREATE INDEX IX_UserPermission_GrantedByUserId ON UserPermission(GrantedByUserId)` - for audit queries

**Tenant Isolation**: ✅ TENANT-SCOPED (via TenantUser.TenantId)

**Use Case**: Grant `delete-users` permission to specific manager without org-admin role

---

### 13. UserStatusType (Status Lookup)

**Purpose**: Lookup table for user account statuses

```csharp
public class UserStatusType
{
    public Guid Id { get; set; }                    // PK (deterministic GUIDs)
    public string Name { get; set; }                // "active", "inactive", "suspended", "pending"
    public string DisplayName { get; set; }         // "Active", "Inactive", "Suspended", "Pending"
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigations
    public ICollection<UserProfile> UserProfiles { get; set; }
}
```

**Constraints**:
- `Name`: Unique, lowercase, max 20 chars
- **SEED DATA**: 4 statuses pre-seeded with deterministic GUIDs

**Relationships**:
- **1:Many** → UserProfile

**Indexes**:
- Recommended: `CREATE UNIQUE INDEX IX_UserStatusType_Name ON UserStatusType(Name)`

**Tenant Isolation**: Global entity (status types are system-wide)

**State Transitions**:
```
pending → active (user completes onboarding)
active → suspended (admin suspends account)
active → inactive (user disabled by admin)
suspended → active (admin reinstates)
inactive → active (admin re-enables)
* → deleted (soft delete via status change)
```

**Seeded Statuses** (see `IdentityTenantManagementContext.cs:184-217`):
1. `active` (90A89389-7891-4784-90DF-F556E95BCCD9)
2. `inactive` (7F313E08-F83E-43DF-B550-C11014592AB7)
3. `suspended` (52EE270F-D6DE-4F96-A578-3C8E84AF4B9B)
4. `pending` (3981B8F5-FFBD-426A-ABDE-A723631B3536)

---

## Infrastructure Entities

### 14. IdentityProvider (External Provider Config)

**Purpose**: Configuration for external identity providers (Keycloak, Auth0, Azure AD)

```csharp
public class IdentityProvider
{
    public Guid Id { get; set; }                    // PK
    public string Name { get; set; }                // "Keycloak", "Auth0", "AzureAD"
    public string ProviderType { get; set; }        // "oidc", "saml", "oauth2"
    public string? BaseUrl { get; set; }            // Provider base URL
    public DateTime CreatedAt { get; set; }

    // Navigations
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; }
}
```

**Constraints**:
- `Name`: Unique, max 50 chars
- `ProviderType`: Enum recommended (oidc, saml, oauth2)
- `BaseUrl`: Nullable (some providers use discovery)

**Relationships**:
- **1:Many** → ExternalIdentity

**Indexes**:
- Recommended: `CREATE UNIQUE INDEX IX_IdentityProvider_Name ON IdentityProvider(Name)`

**Tenant Isolation**: Global entity (providers are system-wide, but can be tenant-specific in future)

**Seeded Provider**:
```csharp
Id: 049284C1-FF29-4F28-869F-F64300B69719
Name: "Keycloak"
ProviderType: "oidc"
BaseUrl: "http://localhost:8080/" // ⚠️ Hardcoded, should be configurable
```

**⚠️ Issue**: BaseUrl hardcoded to localhost - see research.md Section 5 for recommendations

---

### 15. ExternalIdentity (ID Mapping)

**Purpose**: Maps internal IDs (User.Id, Tenant.Id) to external provider IDs (Keycloak UUIDs)

```csharp
public class ExternalIdentity
{
    public Guid Id { get; set; }                    // PK
    public Guid ProviderId { get; set; }            // FK → IdentityProvider
    public Guid EntityTypeId { get; set; }          // FK → ExternalIdentityEntityType
    public Guid EntityId { get; set; }              // Polymorphic: User.Id or Tenant.Id
    public string ExternalIdentifier { get; set; }  // Keycloak UUID
    public DateTime CreatedAt { get; set; }

    // Navigations
    public IdentityProvider Provider { get; set; }
    public ExternalIdentityEntityType EntityType { get; set; }
}
```

**Constraints**:
- `ExternalIdentifier`: Required, max 255 chars
- Polymorphic design: `EntityId` references either `User.Id` OR `Tenant.Id` based on `EntityTypeId`

**Relationships**:
- **Many:1** → IdentityProvider (DeleteBehavior.Restrict)
- **Many:1** → ExternalIdentityEntityType (DeleteBehavior.Restrict)
- Polymorphic reference (not enforced by FK): User or Tenant

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_ExternalIdentity_Provider_ExternalId ON ExternalIdentity(ProviderId, ExternalIdentifier)` - prevent duplicate external IDs
- Recommended: `CREATE INDEX IX_ExternalIdentity_EntityType_EntityId ON ExternalIdentity(EntityTypeId, EntityId)` - for reverse lookups

**Tenant Isolation**: Partially tenant-scoped (when EntityType = "tenant")

**Example Mapping**:
```
Internal User (abc-123-def) → Keycloak User (keycloak-uuid-789)
Internal Tenant (xyz-456-abc) → Keycloak Realm (keycloak-realm-uuid-101)
```

---

### 16. ExternalIdentityEntityType (Polymorphic Type Lookup)

**Purpose**: Defines entity types for polymorphic ExternalIdentity

```csharp
public class ExternalIdentityEntityType
{
    public Guid Id { get; set; }                    // PK (deterministic GUID)
    public string EntityType { get; set; }          // "user" or "tenant"

    // Navigations
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; }
}
```

**Constraints**:
- `EntityType`: Unique, lowercase, max 20 chars
- **SEED DATA**: 2 types pre-seeded

**Relationships**:
- **1:Many** → ExternalIdentity

**Indexes**:
- Recommended: `CREATE UNIQUE INDEX IX_ExternalIdentityEntityType_EntityType ON ExternalIdentityEntityType(EntityType)`

**Tenant Isolation**: Global entity

**Seeded Types**:
1. `user` (86E6890C-0B3F-4278-BD92-A4FD2EA55413)
2. `tenant` (F6D6E7FC-8998-4B77-AD31-552E5C76C3DD)

---

### 17. GlobalSettings (Configuration Key-Value Store)

**Purpose**: System-wide configuration settings

```csharp
public class GlobalSettings
{
    public Guid Id { get; set; }                    // PK
    public string Key { get; set; }                 // Unique setting key
    public string Value { get; set; }               // Setting value
    public string? Description { get; set; }        // Setting description
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Constraints**:
- `Key`: **UNIQUE** (enforced by EF Core), max 100 chars
- `Value`: Max 500 chars (or larger for JSON)
- `Description`: Nullable, max 500 chars

**Relationships**: None

**Indexes**:
- **CRITICAL**: `CREATE UNIQUE INDEX IX_GlobalSettings_Key ON GlobalSettings(Key)` - enforced by EF Core

**Tenant Isolation**: Global entity

**Seeded Settings**:
```csharp
Key: "RequirePermissionToGrant"
Value: "true"
Description: "Users must have permission themselves before granting to others"
```

**Use Cases**:
- Feature flags: `EnableApiVersioning = "true"`
- Limits: `MaxUsersPerTenant = "100"`
- Policies: `RequirePermissionToGrant = "true"`

---

### 18. RegistrationFailureLog (Saga Audit Trail)

**Purpose**: Logs failed registration attempts for saga rollback audit

```csharp
public class RegistrationFailureLog
{
    public Guid Id { get; set; }                    // PK
    public string KeycloakUserId { get; set; }      // Keycloak UUID (may be empty)
    public string KeycloakTenantId { get; set; }    // Keycloak Realm UUID (may be empty)
    public string Email { get; set; }               // User email from registration
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string ErrorMessage { get; set; }        // Exception message
    public string ErrorDetails { get; set; }        // Full exception details
    public bool KeycloakUserRolledBack { get; set; }// True if compensating delete succeeded
    public DateTime FailedAt { get; set; }
}
```

**Constraints**:
- All fields default to empty string (nullable would be better design)
- Max lengths recommended: Email (255), Names (100), ErrorMessage (500), ErrorDetails (4000)

**Relationships**: None (isolated audit log)

**Indexes**:
- Recommended: `CREATE INDEX IX_RegistrationFailureLog_FailedAt ON RegistrationFailureLog(FailedAt DESC)` - for recent failures
- Recommended: `CREATE INDEX IX_RegistrationFailureLog_Email ON RegistrationFailureLog(Email)` - for user-specific failure lookups

**Tenant Isolation**: Not tenant-scoped (system-wide audit log)

**⚠️ Security Concern**: `ErrorDetails` may contain stack traces - see research.md Section 2 (Security Audit) for recommendations

**Use Cases**:
- Saga failure audit trail (RegistrationProcessorService)
- Debugging onboarding issues
- Monitoring registration success rate

---

## Entity Relationship Diagram (ERD)

```
┌─────────────────┐              ┌─────────────────┐
│     Tenant      │──────────────│  TenantDomain   │
│  (Root Tenant)  │   1:Many     │ (Domain Verify) │
└────────┬────────┘              └─────────────────┘
         │
         │ Many:Many (via TenantUser)
         │
┌────────┴────────┐              ┌─────────────────────┐
│      User       │──────────────│  ExternalIdentity   │
│ (Global Identity│   1:Many     │  (Keycloak Mapping) │
└────────┬────────┘              └──────────┬──────────┘
         │                                  │
         │                                  │ Many:1
         │                                  │
         │                       ┌──────────┴──────────────┐
         │                       │  IdentityProvider       │
         │                       │  (Keycloak Config)      │
         │                       └──────────┬──────────────┘
         │                                  │
         │ Many:Many                        │ Many:1
         │                                  │
┌────────┴────────────┐          ┌──────────┴────────────────────┐
│    TenantUser       │          │ ExternalIdentityEntityType    │
│ (Membership)        │          │ (user/tenant)                 │
└─────────┬───────────┘          └───────────────────────────────┘
          │
          │ 1:1
          │
┌─────────┴──────────────┐       ┌─────────────────────┐
│  TenantUserProfile     │───────│   UserProfile       │
│  (Profile Link)        │ Many:1│ (Name & Status)     │
└────────────────────────┘       └──────────┬──────────┘
                                            │
                                            │ Many:1
                                            │
                                 ┌──────────┴──────────────┐
                                 │   UserStatusType        │
                                 │ (active/inactive/etc)   │
                                 └─────────────────────────┘


┌─────────────────┐              ┌─────────────────────┐
│    TenantUser   │──────────────│  TenantUserRole     │
│                 │   1:Many     │ (Role Assignment)   │
└─────────────────┘              └──────────┬──────────┘
                                            │
                                            │ Many:1
                                            │
┌─────────────────┐              ┌──────────┴──────────┐
│      Role       │──────────────│   RolePermission    │
│ (org-admin etc) │   1:Many     │ (Role→Permission)   │
└─────────────────┘              └──────────┬──────────┘
                                            │
                                            │ Many:1
                                            │
┌─────────────────┐              ┌──────────┴──────────┐
│   Permission    │──────────────│  PermissionGroup    │
│ (invite-users)  │   Many:1     │ (SystemAdmin)       │
└────────┬────────┘              └─────────────────────┘
         │
         │ Many:Many
         │
┌────────┴────────────┐
│  UserPermission     │
│ (Direct Grant)      │
└─────────────────────┘


┌─────────────────────────┐
│   GlobalSettings        │
│  (Config Key-Value)     │
└─────────────────────────┘

┌─────────────────────────┐
│ RegistrationFailureLog  │
│  (Saga Audit Trail)     │
└─────────────────────────┘
```

---

## Tenant Isolation Boundaries

**CRITICAL**: All queries for tenant-scoped entities MUST filter by TenantId (directly or via relationships)

### Tenant-Scoped Entities (8)

Entities that MUST enforce tenant context in all queries:

1. **TenantUser** - Filter by `TenantUser.TenantId`
2. **TenantDomain** - Filter by `TenantDomain.TenantId`
3. **TenantUserProfile** - Filter via `TenantUserProfile.TenantUser.TenantId`
4. **TenantUserRole** - Filter via `TenantUserRole.TenantUser.TenantId`
5. **UserPermission** - Filter via `UserPermission.TenantUser.TenantId`
6. **ExternalIdentity** (when EntityType = "tenant") - Filter by `EntityId = Tenant.Id`

**Derived Tenant Queries** (must join to TenantUser):
7. **User** (when querying tenant members) - `FROM User JOIN TenantUser ON ... WHERE TenantUser.TenantId = @tenantId`
8. **UserProfile** (when querying tenant member profiles) - `FROM UserProfile JOIN TenantUserProfile → TenantUser WHERE TenantUser.TenantId = @tenantId`

### Global Entities (10)

Entities that are NOT tenant-scoped:

1. **Tenant** - Root entity (query by Tenant.Id directly)
2. **User** - Global (can belong to multiple tenants)
3. **UserProfile** - Global (can be shared across tenants)
4. **Role** - System-wide role templates
5. **Permission** - System-wide capabilities
6. **PermissionGroup** - System-wide grouping
7. **RolePermission** - System-wide role definitions
8. **UserStatusType** - System-wide lookup
9. **IdentityProvider** - System-wide provider config
10. **ExternalIdentityEntityType** - System-wide lookup
11. **GlobalSettings** - System-wide configuration
12. **RegistrationFailureLog** - System-wide audit log

---

## Recommended Indexes (Performance Optimization)

**CRITICAL Indexes** (enforce uniqueness, prevent table scans):

```sql
-- User uniqueness
CREATE UNIQUE INDEX IX_User_Email ON [User](Email);

-- Tenant membership
CREATE UNIQUE INDEX IX_TenantUser_TenantId_UserId ON TenantUser(TenantId, UserId);
CREATE INDEX IX_TenantUser_TenantId ON TenantUser(TenantId) INCLUDE (UserId);
CREATE INDEX IX_TenantUser_UserId ON TenantUser(UserId) INCLUDE (TenantId);

-- Domain verification
CREATE UNIQUE INDEX IX_TenantDomain_Domain ON TenantDomain(Domain);
CREATE INDEX IX_TenantDomain_TenantId_IsPrimary ON TenantDomain(TenantId, IsPrimary);

-- Profile linking
CREATE UNIQUE INDEX IX_TenantUserProfile_TenantUserId ON TenantUserProfile(TenantUserId);

-- Role assignments
CREATE UNIQUE INDEX IX_TenantUserRole_TenantUserId_RoleId ON TenantUserRole(TenantUserId, RoleId);
CREATE INDEX IX_TenantUserRole_TenantUserId ON TenantUserRole(TenantUserId);

-- Permission assignments
CREATE UNIQUE INDEX IX_UserPermission_TenantUserId_PermissionId ON UserPermission(TenantUserId, PermissionId);

-- Role-Permission mapping
CREATE UNIQUE INDEX IX_RolePermission_RoleId_PermissionId ON RolePermission(RoleId, PermissionId);

-- External ID mapping
CREATE UNIQUE INDEX IX_ExternalIdentity_ProviderId_ExternalId ON ExternalIdentity(ProviderId, ExternalIdentifier);
CREATE INDEX IX_ExternalIdentity_EntityTypeId_EntityId ON ExternalIdentity(EntityTypeId, EntityId);

-- Global settings
CREATE UNIQUE INDEX IX_GlobalSettings_Key ON GlobalSettings([Key]);
```

**Recommended Indexes** (improve query performance):

```sql
-- Temporal queries
CREATE INDEX IX_Tenant_CreatedAt ON Tenant(CreatedAt DESC);
CREATE INDEX IX_User_CreatedAt ON [User](CreatedAt DESC);
CREATE INDEX IX_RegistrationFailureLog_FailedAt ON RegistrationFailureLog(FailedAt DESC);

-- Status filtering
CREATE INDEX IX_Tenant_Status ON Tenant([Status]);
CREATE INDEX IX_UserProfile_StatusId ON UserProfile(StatusId);

-- Name searches
CREATE INDEX IX_UserProfile_LastName_FirstName ON UserProfile(LastName, FirstName);

-- Audit trails
CREATE INDEX IX_UserPermission_GrantedByUserId ON UserPermission(GrantedByUserId);
CREATE INDEX IX_RegistrationFailureLog_Email ON RegistrationFailureLog(Email);
```

**Total Recommended Indexes**: 23 (17 critical, 6 performance)

---

## Validation Rules Summary

### String Length Recommendations

| Entity | Field | Max Length | Validation |
|--------|-------|------------|------------|
| Tenant | Name | 200 | Required |
| User | Email | 255 | Required, Unique, Email format |
| TenantDomain | Domain | 253 | Required, Unique, Domain regex |
| UserProfile | FirstName | 100 | Required |
| UserProfile | LastName | 100 | Required |
| Role | Name | 50 | Required, Unique, Kebab-case |
| Permission | Name | 50 | Required, Unique, Kebab-case |
| PermissionGroup | Name | 50 | Required, Unique, PascalCase |
| UserStatusType | Name | 20 | Required, Unique, Lowercase |
| IdentityProvider | Name | 50 | Required, Unique |
| ExternalIdentity | ExternalIdentifier | 255 | Required |
| GlobalSettings | Key | 100 | Required, Unique |
| GlobalSettings | Value | 500 | Required |
| RegistrationFailureLog | Email | 255 | - |
| RegistrationFailureLog | ErrorDetails | 4000 | - |

### Required Field Rules

**Always Required**:
- All `Id` fields (primary keys)
- All foreign keys (unless explicitly nullable)
- `CreatedAt` timestamps
- Email addresses
- Names (FirstName, LastName, Tenant.Name, Role.Name, etc.)

**Nullable/Optional**:
- `UpdatedAt` timestamps (only set on update)
- `Description` fields
- `GrantedByUserId` in UserPermission
- `BaseUrl` in IdentityProvider

---

## State Transition Diagram

### UserStatusType Lifecycle

```
           ┌──────────┐
           │ pending  │ (User invited, not yet activated)
           └────┬─────┘
                │ Complete onboarding
                ▼
           ┌──────────┐
     ┌─────│  active  │◄────┐
     │     └────┬─────┘     │
     │          │            │ Reinstate
     │ Suspend  │ Disable    │
     ▼          ▼            │
┌─────────┐ ┌──────────┐    │
│suspended│ │ inactive │────┘
└─────────┘ └──────────┘
     │            │
     │  Soft      │  Soft
     │  Delete    │  Delete
     ▼            ▼
  (Status change to "deleted" or physical DELETE)
```

**Transition Rules**:
- `pending → active`: User completes email verification or first login
- `active → suspended`: Admin suspends account (violation, payment issue)
- `active → inactive`: Admin disables account (terminated employee)
- `suspended → active`: Admin reinstates account
- `inactive → active`: Admin re-enables account
- Any → deleted: Soft delete via status change OR hard delete (CASCADE)

---

## Database Migration Notes

**Migration History**: See `IdentityTenantManagementDatabase/Migrations/`
- `20251125004617_InitialCreate.cs` - Initial schema
- `20251125011921_MoveStatusColumnFromUserToUserProfiles.cs` - Moved status to UserProfile

**Applying Migrations**:
```bash
dotnet ef database update --project IdentityTenantManagementDatabase
```

**Creating New Migration**:
```bash
dotnet ef migrations add MigrationName --project IdentityTenantManagementDatabase
```

**Seed Data Location**: `IdentityTenantManagementContext.cs:OnModelCreating` (lines 35-358)

---

## Best Practices for Developers

### Query Patterns

✅ **DO**: Always filter tenant-scoped entities by TenantId
```csharp
var users = await _context.TenantUsers
    .Where(tu => tu.TenantId == tenantId)
    .Include(tu => tu.User)
    .ToListAsync();
```

❌ **DON'T**: Query TenantUser without tenant filter
```csharp
var users = await _context.TenantUsers.ToListAsync(); // EXPOSES ALL TENANTS!
```

✅ **DO**: Use repository pattern with tenant context
```csharp
public interface ITenantUserRepository
{
    Task<List<TenantUser>> GetByTenantIdAsync(Guid tenantId);
}
```

### Soft Delete Considerations

**Current Implementation**: No soft delete (uses UserStatusType = "inactive" or "deleted")

**Recommendation**: Implement soft delete via:
```csharp
public class Tenant
{
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
```

Add global query filter:
```csharp
modelBuilder.Entity<Tenant>().HasQueryFilter(t => !t.IsDeleted);
```

### Audit Trail Enhancement

**Current**: Only `CreatedAt` timestamps

**Recommendation**: Add `CreatedBy`, `UpdatedBy` for full audit:
```csharp
public Guid? CreatedByUserId { get; set; }
public Guid? UpdatedByUserId { get; set; }
```

---

## Summary Statistics

- **Total Entities**: 18
- **Tenant-Scoped Entities**: 8
- **Global Entities**: 10
- **Many-to-Many Relationships**: 4 (TenantUser, RolePermission, TenantUserRole, UserPermission)
- **One-to-One Relationships**: 1 (TenantUser → TenantUserProfile)
- **Seeded Entities**: 5 (IdentityProvider, ExternalIdentityEntityType, UserStatusType, Role, Permission, PermissionGroup, RolePermission, GlobalSettings)
- **Recommended Indexes**: 23 (17 critical, 6 performance)
- **Unique Constraints**: 11

---

**Document Version**: 1.0
**Last Updated**: 2025-11-30
**Related Documents**:
- `specs/master/plan.md` - Implementation plan
- `specs/master/research.md` - Architectural research
- `specs/master/contracts/` - API contracts (to be created)
- `IdentityTenantManagementDatabase/DbContexts/IdentityTenantManagementContext.cs` - EF Core configuration