---

description: "Task list for IdentityTenantManagement platform baseline"
---

# Tasks: IdentityTenantManagement Platform Baseline

**Input**: Platform architecture plan from `specs/master/plan.md`
**Prerequisites**: plan.md (completed), constitution.md (completed)

**Organization**: Tasks are grouped by phase to establish platform baseline before feature development. This is NOT a feature with user stories, but rather platform infrastructure and documentation tasks.

## Format: `[ID] [P?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions

Multi-project C# .NET 9.0 solution:
- **Core API**: `IdentityTenantManagement/`
- **Blazor Demo**: `IdentityTenantManagement.BlazorApp/`
- **Database**: `IdentityTenantManagementDatabase/`
- **Adapter**: `KeycloakAdapter/`
- **Tests**: `IdentityTenantManagement.Tests/`
- **Documentation**: `specs/master/`

---

## Phase 0: Research & Documentation

**Purpose**: Document architectural decisions, audit security, and plan missing components

**Goal**: Produce comprehensive research.md documenting current architecture and future requirements

### Research Tasks

- [X] T001 [P] Create research.md file in specs/master/research.md
- [X] T002 [P] Document Saga Pattern implementation by analyzing IdentityTenantManagement/Services/RegistrationProcessorService.cs
- [X] T003 [P] Document rollback strategies for failed Keycloak operations in RegistrationProcessorService.cs
- [X] T004 Verify idempotency of compensating transactions in saga workflow and document in research.md section "Saga Pattern for Distributed Transactions"
- [X] T005 [P] Audit OnboardingController.cs for SQL injection, XSS, and command injection vulnerabilities
- [X] T006 [P] Audit TenantsController.cs for injection vulnerabilities
- [X] T007 [P] Audit UsersController.cs for injection vulnerabilities
- [X] T008 [P] Audit AuthenticationController.cs for injection vulnerabilities
- [X] T009 Verify RequirePermissionAttribute usage across all controllers (authentication vs authorization check)
- [X] T010 Review GlobalExceptionHandler.cs (IdentityTenantManagement/Middleware/) for information leakage in error responses
- [X] T011 Review RegistrationFailureLog entity (IdentityTenantManagementDatabase/Models/) for sensitive data logging risks
- [X] T012 Validate CORS configuration in IdentityTenantManagement/Program.cs
- [X] T013 Document OWASP Top 10 audit findings in research.md section "Security Compliance Audit"
- [X] T014 [P] Research ASP.NET Core API versioning options (Asp.Versioning.Mvc, URL path vs header vs query string)
- [X] T015 [P] Define deprecation policy (warning period, timeline, breaking change process)
- [X] T016 Plan API versioning implementation for OnboardingController, TenantsController, UsersController, AuthenticationController
- [X] T017 Document API versioning strategy in research.md section "API Versioning Implementation Plan"
- [X] T018 [P] Identify tenant isolation test scenarios (attempt cross-tenant data access via repository methods)
- [X] T019 [P] Define contract test structure for KeycloakAdapter (CreateRealm, CreateUser, AssignRole, DeleteRealm)
- [X] T020 [P] Plan integration tests for OnboardingService saga workflow (success path, Keycloak failure, rollback verification)
- [X] T021 Document test coverage expansion plan in research.md section "Test Coverage Roadmap"
- [X] T022 [P] Review existing seed data implementation in IdentityTenantManagementDatabase (search for OnModelCreating or seed classes)
- [X] T023 Document seed data idempotency guarantees (INSERT vs UPSERT logic) in research.md section "Seed Data Implementation"
- [X] T024 [P] Research payment gateway integration patterns for Stripe and PayPal
- [X] T025 [P] Define product tiering model (plan levels: Free, Pro, Enterprise; feature flags per tier)
- [X] T026 [P] Plan financial reporting requirements (revenue tracking, usage metrics, invoicing)
- [X] T027 Document payment and reporting architecture in research.md section "Payment and Reporting Future Architecture"

**Checkpoint**: research.md complete with all 6 sections (Saga Pattern, Security Audit, API Versioning, Test Coverage, Seed Data, Payment Architecture)

---

## Phase 1: Design Artifacts & Contracts

**Purpose**: Generate detailed design documentation and API contracts

**Prerequisites**: Phase 0 (research.md) complete

**Goal**: Produce data-model.md, contracts/, and quickstart.md for developer onboarding

### Data Model Documentation

- [X] T028 Create data-model.md file in specs/master/data-model.md
- [X] T029 Document Tenant entity (fields, types, nullability, relationships) from IdentityTenantManagementDatabase/Models/Tenant.cs
- [X] T030 [P] Document TenantDomain entity from IdentityTenantManagementDatabase/Models/TenantDomain.cs
- [X] T031 [P] Document TenantUser entity (tenant-user relationship) from IdentityTenantManagementDatabase/Models/TenantUser.cs
- [X] T032 [P] Document TenantUserRole entity from IdentityTenantManagementDatabase/Models/TenantUserRole.cs
- [X] T033 [P] Document TenantUserProfile entity from IdentityTenantManagementDatabase/Models/TenantUserProfile.cs
- [X] T034 [P] Document User entity (global user) from IdentityTenantManagementDatabase/Models/User.cs
- [X] T035 [P] Document UserProfile entity from IdentityTenantManagementDatabase/Models/UserProfile.cs
- [X] T036 [P] Document UserStatusType entity from IdentityTenantManagementDatabase/Models/UserStatusType.cs
- [X] T037 [P] Document Role entity from IdentityTenantManagementDatabase/Models/Role.cs
- [X] T038 [P] Document RolePermission entity from IdentityTenantManagementDatabase/Models/RolePermission.cs
- [X] T039 [P] Document Permission entity from IdentityTenantManagementDatabase/Models/Permission.cs
- [X] T040 [P] Document PermissionGroup entity from IdentityTenantManagementDatabase/Models/PermissionGroup.cs
- [X] T041 [P] Document UserPermission entity from IdentityTenantManagementDatabase/Models/UserPermission.cs
- [X] T042 [P] Document IdentityProvider entity from IdentityTenantManagementDatabase/Models/IdentityProvider.cs
- [X] T043 [P] Document ExternalIdentity entity from IdentityTenantManagementDatabase/Models/ExternalIdentity.cs
- [X] T044 [P] Document ExternalIdentityEntityType entity from IdentityTenantManagementDatabase/Models/ExternalIdentityEntityType.cs
- [X] T045 [P] Document GlobalSettings entity from IdentityTenantManagementDatabase/Models/GlobalSettings.cs
- [X] T046 [P] Document RegistrationFailureLog entity from IdentityTenantManagementDatabase/Models/RegistrationFailureLog.cs
- [X] T047 Add entity relationship diagram (ERD) showing all relationships (one-to-many, many-to-many) to data-model.md
- [X] T048 Document tenant isolation boundaries (which entities are tenant-scoped vs global) in data-model.md
- [X] T049 Document recommended indexes (TenantId, UserId, Email, etc.) for performance in data-model.md
- [X] T050 Document validation rules per entity (required fields, string lengths, regex patterns) in data-model.md
- [X] T051 Document state transitions (UserStatusType lifecycle: Active, Suspended, Deleted) in data-model.md

### API Contracts

- [X] T052 Create contracts directory in specs/master/contracts/
- [X] T053 [P] Generate onboarding-api.yaml OpenAPI spec by analyzing IdentityTenantManagement/Controllers/OnboardingController.cs
- [X] T054 [P] Generate tenants-api.yaml OpenAPI spec by analyzing IdentityTenantManagement/Controllers/TenantsController.cs
- [X] T055 [P] Generate users-api.yaml OpenAPI spec by analyzing IdentityTenantManagement/Controllers/UsersController.cs
- [X] T056 [P] Generate authentication-api.yaml OpenAPI spec by analyzing IdentityTenantManagement/Controllers/AuthenticationController.cs
- [X] T057 Apply API versioning strategy (v1 prefix) to all generated OpenAPI specs in contracts/
- [X] T058 Add request/response schemas with validation rules to all OpenAPI specs
- [X] T059 Add ErrorResponse model schema to all OpenAPI specs (from IdentityTenantManagement/Models/Responses/ErrorResponse.cs)

### Developer Onboarding Guide

- [X] T060 Create quickstart.md file in specs/master/quickstart.md
- [X] T061 Document prerequisites (SQL Server, .NET 9 SDK, Keycloak setup) in quickstart.md
- [X] T062 Document clone and restore dependencies steps (dotnet restore) in quickstart.md
- [X] T063 Document appsettings.json configuration (connection strings, Keycloak config) in quickstart.md
- [X] T064 Document running migrations (dotnet ef database update --project IdentityTenantManagementDatabase) in quickstart.md
- [X] T065 Document starting Core API (dotnet run --project IdentityTenantManagement) in quickstart.md
- [X] T066 Document starting Blazor demo (dotnet run --project IdentityTenantManagement.BlazorApp) in quickstart.md
- [X] T067 Document testing onboarding workflow (create tenant + admin user via Blazor UI or API) in quickstart.md
- [X] T068 Document running tests (dotnet test) in quickstart.md
- [X] T069 Add debugging tips (common errors, logs to check) to quickstart.md

### Agent Context Update

- [X] T070 Run .specify/scripts/powershell/update-agent-context.ps1 -AgentType claude to update agent context with tech stack (C# .NET 9.0, EF Core, Blazor Server, Moq, Saga pattern)

**Checkpoint**: data-model.md, contracts/ (4 YAML files), quickstart.md, and agent context complete

---

## Phase 2: Test Coverage Expansion (Constitutional Requirement)

**Purpose**: Implement missing test coverage to meet constitution requirements

**Prerequisites**: Phase 1 (data-model.md, contracts/) complete

**Goal**: Add tenant isolation tests, KeycloakAdapter contract tests, and OnboardingService integration tests

### Tenant Isolation Tests

- [ ] T071 Create TenantIsolationTests.cs in IdentityTenantManagement.Tests/Integration/
- [ ] T072 [P] Implement test: Attempt to query UserRepository.GetByTenantIdAsync with different tenant ID (should return empty, not other tenant's users)
- [ ] T073 [P] Implement test: Attempt to query TenantUserRepository for Tenant A while authenticated as Tenant B (should fail or return empty)
- [ ] T074 [P] Implement test: Verify RoleRepository only returns roles scoped to current tenant
- [ ] T075 [P] Implement test: Verify PermissionRepository enforces tenant boundaries
- [ ] T076 Implement test: End-to-end API test calling UsersController with Tenant A credentials attempting to access Tenant B users (should return 403 Forbidden or 404 Not Found)

### KeycloakAdapter Contract Tests

- [ ] T077 Create KeycloakAdapterContractTests.cs in IdentityTenantManagement.Tests/Contract/
- [ ] T078 [P] Implement contract test for CreateRealm operation (mock Keycloak API response, verify adapter calls correct endpoint)
- [ ] T079 [P] Implement contract test for CreateUser operation in realm
- [ ] T080 [P] Implement contract test for AssignRole operation
- [ ] T081 [P] Implement contract test for DeleteRealm operation (compensating transaction)
- [ ] T082 [P] Implement contract test for SyncUser operation
- [ ] T083 Verify all contract tests use Moq to mock Keycloak HTTP client, not real Keycloak instance

### OnboardingService Saga Integration Tests

- [ ] T084 Create OnboardingServiceSagaTests.cs in IdentityTenantManagement.Tests/Integration/
- [ ] T085 [P] Implement test: Successful onboarding (tenant created in DB, Keycloak realm created, user created, role assigned)
- [ ] T086 [P] Implement test: Keycloak CreateRealm fails (verify local DB transaction rollback, no tenant persisted)
- [ ] T087 [P] Implement test: Keycloak CreateUser fails after realm created (verify compensating DeleteRealm call, local DB rollback)
- [ ] T088 [P] Implement test: Verify RegistrationFailureLog entry created on saga failure with correct error details
- [ ] T089 Verify all saga tests use in-memory EF Core provider and mocked KeycloakAdapter

**Checkpoint**: All constitutional test coverage requirements met (6 tenant isolation tests, 6 adapter contract tests, 5 saga integration tests)

---

## Phase 3: API Versioning Implementation (Constitutional Requirement)

**Purpose**: Implement API versioning to meet constitutional stability requirements

**Prerequisites**: Phase 0 (research.md API Versioning section) complete

**Goal**: Add versioning middleware and update controllers to support v1 API

### Versioning Infrastructure

- [ ] T090 Add Asp.Versioning.Mvc NuGet package to IdentityTenantManagement project (version 9.0+ for .NET 9 compatibility)
- [ ] T091 Configure API versioning in IdentityTenantManagement/Program.cs (AddApiVersioning, URL path versioning)
- [ ] T092 Configure Swagger to support multiple API versions in Program.cs (AddSwaggerGen with version documents)

### Controller Updates

- [ ] T093 [P] Add [ApiVersion("1.0")] attribute to OnboardingController.cs and update route to [Route("api/v{version:apiVersion}/[controller]")]
- [ ] T094 [P] Add [ApiVersion("1.0")] attribute to TenantsController.cs and update route
- [ ] T095 [P] Add [ApiVersion("1.0")] attribute to UsersController.cs and update route
- [ ] T096 [P] Add [ApiVersion("1.0")] attribute to AuthenticationController.cs and update route

### Versioning Documentation

- [ ] T097 Create API-VERSIONING.md in specs/master/ documenting versioning policy (deprecation period, breaking change process)
- [ ] T098 Update quickstart.md with v1 API URLs (e.g., /api/v1/Onboarding instead of /api/Onboarding)
- [ ] T099 Update BlazorApp OnboardingApiClient.cs to use versioned API URLs (/api/v1/Onboarding)

**Checkpoint**: API versioning fully implemented and documented

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup, documentation, and validation before v1.0 release

**Prerequisites**: Phases 0, 1, 2, 3 complete

### Documentation Validation

- [ ] T100 [P] Validate research.md has all 6 required sections complete
- [ ] T101 [P] Validate data-model.md includes all 18+ entities with complete details
- [ ] T102 [P] Validate contracts/ directory has 4 OpenAPI YAML files with v1 versioning
- [ ] T103 [P] Validate quickstart.md can be followed by a new developer (all commands work)
- [ ] T104 Run all tests (dotnet test) and verify 100% pass rate for new tests added in Phase 2

### Constitution Re-Check

- [ ] T105 Verify Principle IV (Test Coverage) now PASSES (tenant isolation, adapter contract, saga integration tests complete)
- [ ] T106 Verify Principle VII (API Versioning) now PASSES (versioning middleware implemented, controllers updated)
- [ ] T107 Update specs/master/plan.md Constitution Check section to mark both principles as ✅ PASS
- [ ] T108 Update plan.md Platform Status to "✅ Production Ready - All constitutional requirements met"

### Code Cleanup

- [ ] T109 [P] Remove DevCleanupController.cs or add [ApiExplorerSettings(IgnoreApi = true)] to exclude from production Swagger
- [ ] T110 [P] Review all TODO comments in codebase and convert to GitHub issues or remove
- [ ] T111 Verify no hardcoded secrets in appsettings.json (use User Secrets or environment variables)

**Checkpoint**: Platform baseline complete and production-ready

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 0 (Research)**: No dependencies - can start immediately
- **Phase 1 (Design Artifacts)**: Depends on Phase 0 completion (research.md informs design decisions)
- **Phase 2 (Test Coverage)**: Depends on Phase 1 completion (data-model.md and contracts/ inform test structure)
- **Phase 3 (API Versioning)**: Depends on Phase 0 completion (research.md API Versioning section)
- **Phase 4 (Polish)**: Depends on Phases 0, 1, 2, 3 completion

### Parallel Opportunities

**Phase 0 can run 6 sub-tasks in parallel:**
- Saga Pattern research (T002-T004)
- Security Audit (T005-T013)
- API Versioning research (T014-T017)
- Test Coverage planning (T018-T021)
- Seed Data review (T022-T023)
- Payment Architecture research (T024-T027)

**Phase 1 data model documentation (T029-T046) can run 18 entity tasks in parallel** (each entity is independent)

**Phase 1 API contracts (T053-T056) can run 4 controller tasks in parallel** (each controller is independent)

**Phase 2 tenant isolation tests (T072-T075) can run 4 repository tests in parallel**

**Phase 2 adapter contract tests (T078-T082) can run 5 operation tests in parallel**

**Phase 2 saga integration tests (T085-T088) can run 4 scenario tests in parallel**

**Phase 3 controller updates (T093-T096) can run 4 controller changes in parallel**

### Critical Path

```
T001 (create research.md)
  → T002-T027 (research tasks - can parallelize 6 sections)
    → T028 (create data-model.md)
      → T029-T051 (document entities and relationships)
        → T071-T089 (implement test coverage - uses data model)
          → T105-T108 (constitution re-check)

T014-T017 (API versioning research)
  → T090-T096 (implement API versioning)
    → T097-T099 (update documentation)
      → T105-T108 (constitution re-check)
```

---

## Implementation Strategy

### Sequential Approach (Recommended)

1. **Complete Phase 0: Research** (T001-T027)
   - Parallelized research into 6 sections
   - Output: comprehensive research.md
   - Duration: ~2-3 days

2. **Complete Phase 1: Design Artifacts** (T028-T070)
   - Parallelize entity documentation (T029-T046)
   - Parallelize API contract generation (T053-T056)
   - Output: data-model.md, contracts/, quickstart.md
   - Duration: ~3-4 days

3. **Complete Phase 2: Test Coverage** (T071-T089)
   - Parallelize within each test category
   - Output: 17 new tests meeting constitutional requirements
   - Duration: ~4-5 days

4. **Complete Phase 3: API Versioning** (T090-T099)
   - Parallelize controller updates (T093-T096)
   - Output: v1 API with versioning support
   - Duration: ~1-2 days

5. **Complete Phase 4: Polish** (T100-T111)
   - Validate all deliverables
   - Final constitution re-check
   - Output: Production-ready platform baseline
   - Duration: ~1 day

**Total Estimated Duration**: 11-15 days with parallelization opportunities

### Parallel Team Strategy

With multiple developers:

1. **Developer A**: Phase 0 Sections 1-3 (Saga Pattern, Security Audit, API Versioning)
2. **Developer B**: Phase 0 Sections 4-6 (Test Coverage, Seed Data, Payment Architecture)
3. After Phase 0: **Developer A** → Phase 1 (Data Model + Contracts), **Developer B** → Phase 2 (Test Coverage)
4. After Phases 1-2: **Both** → Phase 3 (API Versioning) + Phase 4 (Polish)

**Parallel Estimated Duration**: 8-10 days

---

## Notes

- [P] tasks = different files, no dependencies
- All file paths are absolute or relative to repository root
- Commit after each phase or logical group
- Platform baseline tasks, NOT feature user stories (constitution action items)
- Stop at any checkpoint to validate phase completion
- After Phase 4: Platform ready for feature development (create feature-specific specs in `specs/001-feature-name/`)