<!--
SYNC IMPACT REPORT
==================
Version change: None → 1.0.0
Modified principles: N/A (Initial constitution)
Added sections: All (Initial constitution)
Removed sections: N/A

Templates requiring updates:
✅ plan-template.md - Constitution Check section aligns with defined principles
✅ spec-template.md - Requirements structure aligns with functional and testing requirements
✅ tasks-template.md - Task organization reflects multi-project structure and testing discipline
✅ agent-file-template.md - No agent-specific references requiring updates
✅ checklist-template.md - No updates required

Follow-up TODOs:
- None (all placeholders filled)

Rationale for v1.0.0:
- Initial constitution establishing baseline governance for IdentityTenantManagement
- Defines core principles for multi-tenant B2B SaaS identity management
- Establishes testing, security, and architectural standards
-->

# IdentityTenantManagement Constitution

## Core Principles

### I. Multi-Tenant Isolation (NON-NEGOTIABLE)

All features MUST maintain strict tenant isolation at every layer:
- Database queries MUST filter by TenantId/OrganizationId
- API endpoints MUST validate tenant context from authentication claims
- Cross-tenant data access is prohibited except for system administrators with explicit audit logging
- Tenant relationships (users, permissions, profiles, settings) MUST be scoped correctly

**Rationale**: B2B SaaS applications require absolute data isolation between customers. A breach of tenant boundaries is a critical security failure that can result in data leaks, compliance violations, and loss of customer trust.

### II. Identity Provider Abstraction

The solution MUST support multiple identity providers through a pluggable architecture:
- Currently supports Keycloak via KeycloakAdapter
- Future providers (Auth0, Azure AD, Okta, etc.) MUST integrate via the same contract
- Core API MUST NOT directly reference provider-specific SDKs; all integration flows through adapters
- Configuration MUST support provider selection and SSO requirements per tenant

**Rationale**: Customers have diverse identity infrastructure. Provider lock-in limits adoption. The adapter pattern enables customers to choose their preferred identity solution while maintaining a consistent internal architecture.

### III. Database-First With Migration Safety

All schema changes MUST follow a controlled migration process:
- Entity Framework migrations required for all database changes
- Migrations MUST be tested in development before production deployment
- Seed data MUST be idempotent (safe to run multiple times)
- Breaking schema changes MUST include backward-compatible transition period where feasible

**Rationale**: Multi-tenant databases require careful change management. Failed migrations can impact all tenants. Idempotent seeds prevent duplicate data issues during repeated deployments.

### IV. Test Coverage for Critical Paths (NON-NEGOTIABLE)

The following areas MUST have automated test coverage:
- **Tenant isolation**: Tests verifying cross-tenant data access is prevented
- **Identity provider integration**: Contract tests for adapter implementations
- **User and permission management**: Repository operations and business logic
- **API endpoints**: Contract tests for public API surface
- **Onboarding workflows**: Integration tests for tenant and user creation

**Rationale**: Identity and access management failures have severe security consequences. Automated tests prevent regressions in critical security boundaries and business logic.

### V. Security-First Development

All features touching authentication, authorization, or sensitive data MUST follow security best practices:
- Passwords MUST be handled by the identity provider (never stored in application database)
- API endpoints MUST validate authorization (not just authentication)
- Sensitive operations MUST be logged with audit trails
- Input validation MUST prevent injection attacks (SQL, XSS, command injection)
- OWASP Top 10 vulnerabilities MUST be addressed during code review

**Rationale**: Identity management solutions are high-value targets for attackers. Security vulnerabilities can compromise not just one tenant but the entire platform. Defense-in-depth and secure-by-default are mandatory.

### VI. Explicit Repository Pattern

Data access MUST use repository interfaces with clear ownership:
- Each entity MUST have a corresponding repository interface (e.g., IUserRepository)
- Repositories MUST encapsulate tenant-scoped queries
- Business logic MUST NOT directly reference DbContext
- Repository implementations MUST be testable via in-memory or mock implementations

**Rationale**: The repository pattern provides a clear boundary for testing, enables tenant filtering at a centralized layer, and makes data access patterns explicit and auditable.

### VII. API Versioning and Stability

Public APIs MUST maintain backward compatibility:
- Breaking changes require API version increment
- Deprecation warnings MUST precede removal by at least one major version
- Internal APIs (between IdentityTenantManagement and BlazorApp) may evolve more freely but MUST be documented

**Rationale**: B2B SaaS customers integrate via APIs. Breaking changes without versioning cause customer outages and erode trust.

## Project Structure and Responsibilities

The solution is organized into 5 projects with clear boundaries:

### 1. IdentityTenantManagement (Core Web API)
**Responsibility**: Orchestrate identity provider integration with internal database state
- Public REST API for tenant onboarding, user management, permissions
- Communicates with Keycloak (or other providers) via adapters
- Persists tenant metadata, user profiles, permission mappings in IdentityTenantManagementDatabase
- Enforces tenant isolation and authorization rules

### 2. IdentityTenantManagement.BlazorApp (Demo Application)
**Responsibility**: Demonstrate solution capabilities via functional UI
- Provides onboarding workflow UI
- Showcases integration patterns for customers
- MUST NOT contain business logic (delegates to Core API)
- Serves as reference implementation for customers building their own UIs

### 3. IdentityTenantManagement.Tests (Test Suite)
**Responsibility**: Validate core functionality and security boundaries
- Unit tests for repositories and services
- Integration tests for tenant isolation
- Contract tests for API endpoints
- Mock identity provider for testing without external dependencies

### 4. IdentityTenantManagementDatabase (Database Project)
**Responsibility**: Define schema, migrations, and seed data
- Entity Framework Core models
- Migration history
- Seed data for development and testing
- Database initialization scripts

### 5. KeycloakAdapter (Identity Provider Wrapper)
**Responsibility**: Encapsulate Keycloak-specific integration
- Wrapper around Keycloak OpenAPI client
- Translates between Keycloak concepts and application domain
- Future providers will have sibling adapters (Auth0Adapter, AzureADAdapter, etc.)

**Constraint**: No more than 5 top-level projects without constitutional amendment justification. Additional projects must demonstrate necessity (e.g., shared library extracted due to reuse across multiple consumers).

## Development Workflow

### Code Review Requirements
- All PRs MUST pass automated tests
- Security-sensitive changes (auth, permissions, tenant isolation) MUST have explicit security review
- Database migrations MUST be reviewed for backward compatibility
- API contract changes MUST be reviewed for versioning compliance

### Testing Gates
- Unit tests MUST pass before merge
- Integration tests SHOULD pass (failures require documented investigation)
- Manual testing REQUIRED for onboarding and SSO workflows (until automated e2e tests exist)

### Deployment Process
- Database migrations run before application deployment
- Configuration changes (appsettings.json) validated in staging before production
- Identity provider configuration changes tested in isolated tenant first

## Governance

This constitution supersedes all other practices and establishes the architectural and security foundation for IdentityTenantManagement.

### Amendment Process
1. Propose change with rationale and impact analysis
2. Review against existing features for breaking changes
3. Document migration plan if existing code violates new principle
4. Increment version according to semantic versioning rules (see below)
5. Update dependent templates (plan-template.md, spec-template.md, tasks-template.md)

### Versioning Policy
- **MAJOR**: Backward incompatible governance change (e.g., removing multi-tenant isolation exception, mandating new test category)
- **MINOR**: New principle added or existing principle materially expanded (e.g., adding new security requirement)
- **PATCH**: Clarifications, typo fixes, non-semantic refinements

### Compliance Review
- All features MUST verify compliance with applicable principles during planning phase (Constitution Check in plan-template.md)
- Violations MUST be explicitly justified in Complexity Tracking section of implementation plan
- Complexity without justification is grounds for rejecting the plan

### Runtime Guidance
Developers should reference this constitution during:
- Feature specification (/speckit.specify)
- Implementation planning (/speckit.plan - Constitution Check section)
- Task generation (/speckit.tasks)
- Code review and pull request approval

**Version**: 1.0.0 | **Ratified**: 2025-11-30 | **Last Amended**: 2025-11-30