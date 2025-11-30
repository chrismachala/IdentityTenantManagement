# Implementation Plan: IdentityTenantManagement Platform

**Branch**: `master` | **Date**: 2025-11-30 | **Spec**: N/A (Overall Platform Architecture)
**Input**: Overall project technical architecture and design patterns

**Note**: This plan documents the overall platform architecture, not a specific feature. Feature-specific plans will be created in their own spec directories.

## Summary

IdentityTenantManagement is a B2B SaaS platform providing multi-tenant identity and access management capabilities. The platform enables customers to spin up tenant-ready applications with configurable identity providers (currently Keycloak, with planned support for Auth0, Azure AD, Okta), user management, permission systems, and onboarding workflows. The solution abstracts identity provider complexity through adapters while maintaining strict tenant isolation and providing a control panel for SaaS businesses to manage authentication, authorization, users, permissions, product tiering, payments, and reporting.

## Technical Context

**Language/Version**: C# .NET 9.0
**Primary Dependencies**: ASP.NET Core 9.0, Entity Framework Core 9.0.9, Blazor Server, Swashbuckle (Swagger), Newtonsoft.Json, Moq (testing)
**Storage**: SQL Server via Entity Framework Core (Code-First approach)
**Testing**: xUnit (inferred from .NET standard), Moq for mocking, in-memory EF Core providers for repository testing
**Target Platform**: Windows/Linux server, containerized deployment ready
**Project Type**: Multi-project solution (Web API + Blazor Web + Database + Adapter + Tests)
**Performance Goals**: Support 1000+ concurrent users per tenant, <500ms API response times for standard operations, handle 100+ tenants per deployment
**Constraints**: Multi-tenant data isolation (critical), identity provider agnostic architecture, backward-compatible API versioning
**Scale/Scope**: B2B SaaS platform supporting multiple tenants, configurable identity providers, extensible permission system

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Multi-Tenant Isolation (NON-NEGOTIABLE)
**Status**: ✅ PASS
**Evidence**:
- All repositories implement tenant-scoped queries via TenantId/OrganizationId filtering
- TenantUser, TenantUserProfile, TenantUserRole models enforce tenant relationships
- Unit of Work pattern ensures transactional consistency across tenant operations
- Constitution mandates all queries MUST filter by tenant context

### II. Identity Provider Abstraction
**Status**: ✅ PASS
**Evidence**:
- KeycloakAdapter project provides isolation from Keycloak-specific SDK
- Core API references KeycloakAdapter, not direct Keycloak libraries
- Architecture supports future adapters (Auth0Adapter, AzureADAdapter, etc.)
- IdentityProvider model in database supports configuration per tenant

### III. Database-First With Migration Safety
**Status**: ✅ PASS
**Evidence**:
- Entity Framework Code-First approach with migrations (e.g., 20251125004617_InitialCreate.cs)
- Migrations include Designer snapshots for safety
- IdentityTenantManagementContextFactory supports design-time migrations
- Seed data implementation assumed idempotent (must verify in Phase 0)

### IV. Test Coverage for Critical Paths (NON-NEGOTIABLE)
**Status**: ⚠️ PARTIAL - REQUIRES EXPANSION
**Evidence**:
- IdentityTenantManagement.Tests project exists
- Moq dependency indicates mocking capability for repositories
- **MISSING**: Explicit tenant isolation tests (verify cross-tenant access prevention)
- **MISSING**: Contract tests for KeycloakAdapter
- **MISSING**: Integration tests for onboarding workflows
- **ACTION REQUIRED**: Expand test coverage to meet constitutional requirements

### V. Security-First Development
**Status**: ✅ PASS WITH VERIFICATION NEEDED
**Evidence**:
- GlobalExceptionHandler middleware for secure error handling
- RequirePermissionAttribute for authorization checks
- ValidationException and NotFoundException for input validation
- RegistrationFailureLog for audit trails
- Passwords delegated to Keycloak (identity provider handles credential storage)
- **VERIFICATION NEEDED**: OWASP Top 10 compliance audit (Phase 0 task)

### VI. Explicit Repository Pattern
**Status**: ✅ PASS
**Evidence**:
- IRepository<T> base interface
- Dedicated repository interfaces: IUserRepository, ITenantRepository, IRoleRepository, IPermissionRepository, etc.
- Repository implementations: UserRepository, TenantRepository, RoleRepository, etc.
- IUnitOfWork interface with UnitOfWork implementation
- Business logic services (OnboardingService, TenantOrchestrationService, UserOrchestrationService) use repositories, not DbContext directly

### VII. API Versioning and Stability
**Status**: ⚠️ NOT YET IMPLEMENTED
**Evidence**:
- Controllers exist (OnboardingController, TenantsController, UsersController, AuthenticationController)
- Swagger/OpenAPI configured (Swashbuckle packages)
- **MISSING**: API versioning middleware/attributes
- **MISSING**: Deprecation policy documentation
- **ACTION REQUIRED**: Implement API versioning before first production release

### Gate Summary
**Overall Status**: ⚠️ CONDITIONAL PASS - 2 action items required before feature work:
1. Expand test coverage for tenant isolation, adapter contracts, and onboarding workflows
2. Implement API versioning strategy

**Existing architectural patterns PASS constitution requirements.** Action items are additions, not fixes.

## Project Structure

### Documentation (master branch - platform overview)

```text
specs/master/
├── plan.md              # This file (platform architecture)
├── research.md          # Phase 0 output (design pattern justifications, security audit)
├── data-model.md        # Phase 1 output (complete entity relationship diagram)
├── quickstart.md        # Phase 1 output (developer onboarding guide)
└── contracts/           # Phase 1 output (OpenAPI specs for all controllers)
```

### Source Code (repository root)

```text
IdentityTenantManagement/
├── IdentityTenantManagement/                  # Core Web API
│   ├── Controllers/
│   │   ├── OnboardingController.cs            # Tenant/user onboarding endpoints
│   │   ├── TenantsController.cs               # Tenant management CRUD
│   │   ├── UsersController.cs                 # User management endpoints
│   │   ├── AuthenticationController.cs        # Auth flow integration
│   │   └── DevCleanupController.cs            # Development utilities
│   ├── Services/
│   │   ├── OnboardingService.cs               # Orchestrates tenant+user creation
│   │   ├── TenantOrchestrationService.cs      # Tenant lifecycle management
│   │   ├── UserOrchestrationService.cs        # User lifecycle management
│   │   ├── RegistrationProcessorService.cs    # Saga coordination for registration
│   │   ├── RoleService.cs                     # Role management business logic
│   │   ├── PermissionService.cs               # Permission management business logic
│   │   ├── UserService.cs                     # User business logic
│   │   └── ServiceCollectionExtensions.cs     # DI configuration
│   ├── Models/
│   │   ├── Onboarding/                        # Onboarding DTOs
│   │   ├── Organisations/                     # Tenant DTOs
│   │   ├── Responses/                         # API response models
│   │   └── Helpers/                           # Shared models
│   ├── Middleware/
│   │   └── GlobalExceptionHandler.cs          # Centralized error handling
│   ├── Authorization/
│   │   └── RequirePermissionAttribute.cs      # Permission-based authorization
│   ├── Exceptions/
│   │   ├── NotFoundException.cs
│   │   └── ValidationException.cs
│   ├── Constants/
│   │   └── ExternalIdentityEntityTypeIds.cs
│   ├── Program.cs                             # Application startup
│   └── appsettings.json                       # Configuration
│
├── IdentityTenantManagement.BlazorApp/        # Demo Web Application
│   ├── Components/
│   │   ├── Layout/                            # Layout components
│   │   └── Pages/
│   │       └── Onboarding.razor               # Onboarding form UI
│   ├── Services/
│   │   └── OnboardingApiClient.cs             # HTTP client for Core API
│   ├── Program.cs
│   └── appsettings.json
│
├── IdentityTenantManagementDatabase/          # Database Project
│   ├── DbContexts/
│   │   ├── IdentityTenantManagementContext.cs      # Main EF DbContext
│   │   └── IdentityTenantManagementContextFactory.cs # Design-time factory
│   ├── Models/                                # EF Core entities
│   │   ├── Tenant.cs                          # Tenant/organization entity
│   │   ├── TenantDomain.cs                    # Tenant domain configuration
│   │   ├── TenantUser.cs                      # Tenant-user relationship
│   │   ├── TenantUserRole.cs                  # Tenant-scoped user roles
│   │   ├── TenantUserProfile.cs               # Tenant-specific user profiles
│   │   ├── User.cs                            # Global user entity
│   │   ├── UserProfile.cs                     # Global user profile
│   │   ├── UserStatusType.cs                  # User status lookup
│   │   ├── Role.cs                            # Role definitions
│   │   ├── RolePermission.cs                  # Role-permission mapping
│   │   ├── Permission.cs                      # Permission definitions
│   │   ├── PermissionGroup.cs                 # Permission grouping
│   │   ├── UserPermission.cs                  # User-specific permissions
│   │   ├── IdentityProvider.cs                # Identity provider configuration
│   │   ├── ExternalIdentity.cs                # External ID mapping (Keycloak IDs)
│   │   ├── ExternalIdentityEntityType.cs      # Entity type lookup
│   │   ├── GlobalSettings.cs                  # System configuration
│   │   └── RegistrationFailureLog.cs          # Audit log for failed registrations
│   ├── Repositories/                          # Repository pattern implementation
│   │   ├── IRepository.cs                     # Base repository interface
│   │   ├── IUnitOfWork.cs                     # Unit of Work interface
│   │   ├── UnitOfWork.cs                      # Unit of Work implementation
│   │   ├── IUserRepository.cs / UserRepository.cs
│   │   ├── ITenantRepository.cs / TenantRepository.cs
│   │   ├── ITenantUserRepository.cs / TenantUserRepository.cs
│   │   ├── IRoleRepository.cs / RoleRepository.cs
│   │   ├── IPermissionRepository.cs / PermissionRepository.cs
│   │   ├── IUserProfileRepository.cs / UserProfileRepository.cs
│   │   ├── ITenantUserProfileRepository.cs / TenantUserProfileRepository.cs
│   │   ├── IIdentityProviderRepository.cs / IdentityProviderRepository.cs
│   │   ├── IExternalIdentityRepository.cs / ExternalIdentityRepository.cs
│   │   ├── IUserStatusTypeRepository.cs / UserStatusTypeRepository.cs
│   │   └── IRegistrationFailureLogRepository.cs / RegistrationFailureLogRepository.cs
│   └── Migrations/                            # EF Core migrations
│       ├── 20251125004617_InitialCreate.cs
│       ├── 20251125011921_MoveStatusColumnFromUserToUserProfiles.cs
│       └── IdentityTenantManagementContextModelSnapshot.cs
│
├── KeycloakAdapter/                           # Keycloak Integration Adapter
│   └── (Wrapper around Keycloak OpenAPI client)
│
├── IdentityTenantManagement.Tests/            # Test Suite
│   └── (Unit tests, integration tests, contract tests)
│
└── IdentityTenantManagement.sln               # Solution file
```

**Structure Decision**: Multi-project solution using "Web application with separate database project" pattern. This structure supports:
- Clear separation of concerns (API, UI, Data, Adapters, Tests)
- Independent deployment of API and Blazor demo
- Database project enables design-time migrations and schema management
- Adapter pattern isolates identity provider dependencies
- Test project validates all layers

## Complexity Tracking

**Note**: No constitution violations exist. This section documents architectural patterns that exceed "simple CRUD" but are justified by B2B SaaS requirements.

| Pattern | Why Needed | Simpler Alternative Rejected Because |
|---------|------------|-------------------------------------|
| 5 top-level projects | Clear boundaries: API, Demo, Database, Adapter, Tests | Monolith would couple identity provider SDK with business logic; breaks Identity Provider Abstraction principle |
| Repository + Unit of Work | Tenant-scoped queries, testability, audit trail | Direct DbContext access cannot guarantee tenant filtering; breaks Multi-Tenant Isolation principle |
| Saga pattern (RegistrationProcessorService) | Distributed transaction coordination between Keycloak and local database | Two-phase commit not supported; manual rollback required for Keycloak failures |
| Orchestration Services | Coordinate multi-step workflows across repositories and adapters | Controllers handling complex workflows violate SRP; difficult to test |
| Separate TenantUser and User entities | Global user identity vs tenant-scoped membership | Single User entity cannot model users belonging to multiple tenants (B2B requirement) |

All patterns align with constitution principles and are necessary for multi-tenant B2B SaaS architecture.

## Phase 0: Outline & Research

**Status**: PENDING

Research tasks to be completed:

1. **Saga Pattern Implementation Review**
   - Document how RegistrationProcessorService implements saga for Keycloak transactions
   - Identify rollback strategies for failed Keycloak operations
   - Verify idempotency of compensating transactions
   - **Output**: research.md section "Saga Pattern for Distributed Transactions"

2. **Security Audit (OWASP Top 10)**
   - Audit all controllers for injection vulnerabilities (SQL, XSS, command injection)
   - Verify authentication vs authorization (RequirePermissionAttribute usage)
   - Review GlobalExceptionHandler for information leakage
   - Check RegistrationFailureLog for sensitive data logging
   - Validate CORS configuration in Program.cs
   - **Output**: research.md section "Security Compliance Audit"

3. **API Versioning Strategy**
   - Research ASP.NET Core API versioning options (URL path, query string, header)
   - Define deprecation policy (warning period, timeline)
   - Plan versioning for existing controllers (OnboardingController, TenantsController, etc.)
   - **Output**: research.md section "API Versioning Implementation Plan"

4. **Test Coverage Expansion Plan**
   - Identify tenant isolation test scenarios (cross-tenant access attempts)
   - Define contract test structure for KeycloakAdapter
   - Plan integration tests for OnboardingService saga workflow
   - **Output**: research.md section "Test Coverage Roadmap"

5. **Seed Data Idempotency Verification**
   - Review existing seed data implementation
   - Document idempotency guarantees (INSERT vs UPSERT logic)
   - Identify risk areas for duplicate data on re-run
   - **Output**: research.md section "Seed Data Implementation"

6. **Payment and Financial Reporting Architecture** (Future)
   - Research payment gateway integration patterns (Stripe, PayPal)
   - Define product tiering model (plan levels, feature flags)
   - Plan financial reporting requirements (revenue, usage tracking)
   - **Output**: research.md section "Payment and Reporting Future Architecture"

**Prerequisites**: None (can start immediately)
**Output**: `specs/master/research.md` with all sections above

## Phase 1: Design & Contracts

**Prerequisites**: research.md complete

### Deliverables

1. **data-model.md** - Complete Entity Relationship Diagram
   - All 18+ entities with fields, types, nullability
   - Relationships (one-to-many, many-to-many) with foreign keys
   - Tenant isolation boundaries (which entities are tenant-scoped)
   - Indexes for performance (TenantId, UserId, etc.)
   - Validation rules per entity
   - State transitions (UserStatusType lifecycle, etc.)

2. **contracts/** - OpenAPI Specifications
   - `/contracts/onboarding-api.yaml` - OnboardingController endpoints
   - `/contracts/tenants-api.yaml` - TenantsController endpoints
   - `/contracts/users-api.yaml` - UsersController endpoints
   - `/contracts/authentication-api.yaml` - AuthenticationController endpoints
   - Versioning strategy applied (v1 prefix)
   - Request/response schemas with validation rules
   - Error response formats (ErrorResponse model)

3. **quickstart.md** - Developer Onboarding Guide
   - Prerequisites (SQL Server, .NET 9 SDK, Keycloak setup)
   - Clone and restore dependencies
   - Configure appsettings.json (connection strings, Keycloak config)
   - Run migrations (`dotnet ef database update`)
   - Start Core API and Blazor demo
   - Test onboarding workflow (create tenant + admin user)
   - Run tests (`dotnet test`)
   - Debugging tips (common errors, logs to check)

4. **Agent Context Update**
   - Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType claude`
   - Add C# .NET 9.0, EF Core, Blazor Server, Moq, Saga pattern to agent context
   - Preserve manual additions between markers

### Constitution Re-Check Post-Design

After Phase 1 completion, re-evaluate:
- ✅ Tenant isolation boundaries documented in data-model.md
- ⚠️ Test coverage expansion tasks identified (implement before v1.0 release)
- ⚠️ API versioning contracts generated with v1 prefix (implement middleware in next sprint)
- ✅ Security audit findings documented in research.md

**Phase 1 Output**: data-model.md, contracts/, quickstart.md, updated agent context

## Architectural Patterns Summary

### Repository + Unit of Work Pattern

**Purpose**: Encapsulate data access with tenant-scoped queries and transactional consistency

**Structure**:
```csharp
// Base abstraction
IRepository<T> : GetById, GetAll, Add, Update, Delete
IUnitOfWork : Repositories, SaveChangesAsync, transaction management

// Concrete repositories (example)
IUserRepository : IRepository<User>
  + GetByEmailAsync(email)
  + GetByTenantIdAsync(tenantId)  // Tenant-scoped query

UserRepository : IUserRepository
  - _context : IdentityTenantManagementContext
  + Implements IUserRepository with EF Core queries
  + All queries include tenant filtering where applicable
```

**Usage in Services**:
```csharp
OnboardingService(IUnitOfWork unitOfWork)
  - unitOfWork.TenantRepository.Add(tenant)
  - unitOfWork.UserRepository.Add(user)
  - unitOfWork.SaveChangesAsync()  // Atomic commit
```

### Saga Pattern for Distributed Transactions

**Purpose**: Coordinate Keycloak operations (external system) with local database transactions

**Implementation** (RegistrationProcessorService):
1. Begin local transaction
2. Create tenant in local database
3. Call KeycloakAdapter.CreateRealm(tenant)
4. If Keycloak succeeds: Create user in Keycloak
5. If Keycloak succeeds: Commit local transaction
6. If any step fails: Rollback local transaction + compensating Keycloak calls (delete realm)
7. Log failure to RegistrationFailureLog for audit

**Rationale**: Keycloak API doesn't support distributed transactions; saga ensures eventual consistency with compensation logic.

### Orchestration Services

**Purpose**: Coordinate multi-step business workflows across repositories and adapters

**Examples**:
- **OnboardingService**: Tenant creation → Admin user creation → Keycloak realm setup → Role assignment
- **TenantOrchestrationService**: Tenant lifecycle (suspend, activate, delete) → Cascade to users, roles, permissions
- **UserOrchestrationService**: User lifecycle → Keycloak sync → Permission updates → Audit logging

**Pattern**: Services use IUnitOfWork for data access, KeycloakAdapter for identity provider, return typed results (not direct HTTP responses).

### Adapter Pattern for Identity Providers

**Purpose**: Isolate Keycloak-specific SDK from core business logic

**Structure**:
```
Core API → IIdentityProviderAdapter → KeycloakAdapter → Keycloak OpenAPI SDK
```

**Future Expansion**:
```
Core API → IIdentityProviderAdapter
             ├── KeycloakAdapter (current)
             ├── Auth0Adapter (planned)
             ├── AzureADAdapter (planned)
             └── OktaAdapter (planned)
```

**Contract** (IIdentityProviderAdapter - to be defined):
- CreateRealm(tenant)
- CreateUser(user, realmId)
- AssignRole(userId, roleId)
- DeleteRealm(realmId)
- SyncUser(user)

## Next Steps

1. **Complete Phase 0 Research** (specs/master/research.md)
2. **Complete Phase 1 Design Artifacts** (data-model.md, contracts/, quickstart.md)
3. **Implement Constitution Action Items**:
   - Add tenant isolation tests to IdentityTenantManagement.Tests
   - Add contract tests for KeycloakAdapter
   - Add integration tests for OnboardingService saga
   - Implement API versioning middleware
4. **Use `/speckit.tasks`** to generate actionable task list for action items
5. **Feature Development**: Once platform baseline is complete, create feature-specific specs (e.g., `specs/001-payment-integration/`, `specs/002-product-tiering/`)

---

**Platform Status**: ⚠️ Architecture complete, 2 action items required before v1.0 production readiness (test coverage + API versioning)