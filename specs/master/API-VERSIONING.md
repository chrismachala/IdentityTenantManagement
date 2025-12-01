# API Versioning Policy

**Version**: 1.0.0
**Last Updated**: 2025-12-01
**Status**: Active

## Overview

This document defines the API versioning strategy for the IdentityTenantManagement platform. All breaking changes to the public API must follow this policy to ensure client stability and predictable upgrade paths.

**Constitutional Requirement**: This policy implements Principle VII - API Versioning and Stability.

---

## Versioning Strategy

### URL Path Versioning

The platform uses **URL path versioning** with the format:

```
/api/v{version}/{controller}/{action}
```

**Examples**:
- `/api/v1/Onboarding/OnboardOrganisation`
- `/api/v1/Tenants/Create`
- `/api/v1/Users/Create`
- `/api/v1/Authentication/login`

### Version Format

- **Major version only**: `v1`, `v2`, `v3`
- **No minor/patch versions in URL**: Internal updates that maintain backward compatibility do NOT require URL changes
- **Default version**: When version is unspecified, API defaults to `v1`

---

## What Constitutes a Breaking Change

A **breaking change** requires a new major version. Examples include:

### Request/Response Breaking Changes
- ❌ Removing a required field from request model
- ❌ Changing field name or type in request/response
- ❌ Removing an endpoint
- ❌ Changing HTTP method (POST → PUT)
- ❌ Adding a new required parameter (without default)
- ❌ Changing authentication/authorization requirements (making endpoint more restrictive)

### Behavior Breaking Changes
- ❌ Changing error response format
- ❌ Changing default values in non-obvious ways
- ❌ Altering side effects (e.g., endpoint previously created 1 record, now creates 2)
- ❌ Changing validation rules to be more strict

### Non-Breaking Changes (Safe to Deploy)
- ✅ Adding new optional fields to request model
- ✅ Adding new fields to response model
- ✅ Adding new endpoints
- ✅ Making authorization less restrictive
- ✅ Bug fixes that correct unintended behavior
- ✅ Performance improvements
- ✅ Internal refactoring

---

## Deprecation Policy

### Timeline

When a breaking change is necessary:

1. **Announcement** (T+0):
   - Announce deprecation in release notes
   - Add `Deprecated` attribute to affected endpoints
   - Update API documentation with deprecation notice

2. **Warning Period** (6 months minimum):
   - Old version remains fully functional
   - API responses include `X-API-Deprecated` header with sunset date
   - Swagger UI displays deprecation warnings
   - Migration guide published

3. **Sunset** (T+6 months):
   - Old version endpoints return `410 Gone` status
   - Sunset announcement sent to all registered API consumers
   - Migration guide remains available

### Example Timeline

```
2025-12-01: v2 released, v1 deprecated
  ↓ 6 months warning period
2026-06-01: v1 sunset (410 Gone), v2 becomes primary
```

### Deprecation Response Headers

Deprecated endpoints will include:

```http
X-API-Deprecated: true
X-API-Sunset-Date: 2026-06-01
X-API-Deprecation-Info: https://docs.example.com/api/deprecation/v1
```

---

## Breaking Change Process

### 1. Proposal Phase

**Owner**: Developer proposing the change

**Actions**:
- Document why the breaking change is necessary
- Identify all affected endpoints
- Estimate impact on existing clients
- Propose migration path

**Approval Required**: Technical Lead + Product Owner

### 2. Design Phase

**Owner**: API Team

**Actions**:
- Design new API version (e.g., v2)
- Create migration guide
- Update OpenAPI specifications
- Plan backward compatibility shims if possible

**Deliverables**:
- New OpenAPI spec (`specs/master/contracts/v2/`)
- Migration guide (`docs/migrations/v1-to-v2.md`)
- Deprecation timeline

### 3. Implementation Phase

**Owner**: Development Team

**Actions**:
- Implement new version alongside old version
- Add `[ApiVersion("2.0")]` attributes
- Update Swagger configuration for multiple versions
- Add deprecation headers to old version

**Testing Requirements**:
- Both versions must pass all tests
- Integration tests verify backward compatibility during transition
- Load testing confirms no performance regression

### 4. Communication Phase

**Owner**: Product/DevRel Team

**Actions** (at announcement):
- Publish release notes with deprecation notice
- Email all registered API consumers
- Update public documentation
- Post on status page/blog

**Actions** (3 months before sunset):
- Send reminder emails
- Log deprecation warnings for active v1 users
- Offer migration support

**Actions** (1 month before sunset):
- Final warning email
- Log critical warnings
- Reach out to remaining v1 users directly

### 5. Sunset Phase

**Owner**: Operations Team

**Actions**:
- Deploy sunset logic (410 Gone responses)
- Monitor for unexpected v1 traffic
- Keep sunset endpoint active for 3 months (error only, no functionality)
- Full removal after 9 months total (6 months warning + 3 months sunset)

---

## Version Lifecycle

### Active Support

- **Current version** (v1): Full support, bug fixes, security patches
- **Previous version** (n/a yet): Security patches only during deprecation period

### Version Status Table

| Version | Status | Release Date | Deprecation Date | Sunset Date |
|---------|--------|--------------|------------------|-------------|
| v1      | Active | 2025-12-01   | N/A              | N/A         |

---

## Implementation Details

### ASP.NET Core Configuration

**Package**: `Asp.Versioning.Mvc` 9.0.0

**Program.cs Configuration**:
```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});
```

**Controller Decoration**:
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TenantsController : ControllerBase
{
    // Endpoints automatically include /api/v1/Tenants/...
}
```

### Multiple Version Support

When v2 is introduced:

```csharp
[ApiController]
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TenantsController : ControllerBase
{
    [HttpPost("Create")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateTenantV1([FromBody] CreateTenantModelV1 body)
    {
        // Old implementation
    }

    [HttpPost("Create")]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> CreateTenantV2([FromBody] CreateTenantModelV2 body)
    {
        // New implementation
    }
}
```

### Swagger Configuration

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Identity Tenant Management API",
        Version = "v1",
        Description = "Version 1 - Active"
    });

    // When v2 is added:
    options.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "Identity Tenant Management API",
        Version = "v2",
        Description = "Version 2 - Latest"
    });
});
```

---

## Client Migration Guide Template

When introducing a new version, provide:

### 1. What Changed
- List of breaking changes with before/after examples

### 2. Migration Steps
- Step-by-step upgrade instructions
- Code samples for common scenarios

### 3. Testing Checklist
- Validation steps to confirm successful migration

### 4. Rollback Plan
- How to revert to old version if needed
- Data compatibility considerations

**Example** (for future v1→v2 migration):

```markdown
## Migrating from v1 to v2

### Change #1: TenantUserOnboardingModel restructure

**v1**:
```json
{
  "createTenantModel": { "name": "Acme", "domain": "acme" },
  "createUserModel": { "email": "user@acme.com", ... }
}
```

**v2**:
```json
{
  "tenant": { "name": "Acme", "domain": "acme" },
  "administrator": { "email": "user@acme.com", ... }
}
```

**Migration**: Update request payload field names.
```

---

## Versioning Best Practices

### For API Developers

1. **Avoid breaking changes when possible**
   - Add optional parameters instead of required ones
   - Extend models instead of replacing them
   - Use feature flags for gradual rollouts

2. **Design for extensibility**
   - Use DTOs (Data Transfer Objects) instead of entity models
   - Include `_links` or `_meta` fields for future enhancements
   - Document extensibility points

3. **Maintain backward compatibility**
   - Test both old and new clients against new deployments
   - Use polymorphic deserialization for flexible request handling
   - Provide default values for new fields

### For API Consumers

1. **Always specify version explicitly**
   - Use `/api/v1/...` not `/api/...`
   - Don't rely on default version behavior

2. **Monitor deprecation headers**
   - Check `X-API-Deprecated` in responses
   - Plan migrations proactively

3. **Subscribe to API changelog**
   - Register for deprecation notifications
   - Review migration guides before sunset dates

---

## Exceptions to Policy

### Emergency Security Fixes

If a security vulnerability requires an immediate breaking change:

1. **Abbreviated Timeline**: 30-day warning instead of 6 months
2. **Critical Notification**: Email + status page alert
3. **Hotfix Support**: Backport fix to all supported versions if possible

### Internal APIs

APIs used only by first-party clients (e.g., BlazorApp ↔ Core API):
- May use faster deprecation cycles
- Must still maintain at least 2-week warning period
- Breaking changes must coincide with client deployments

---

## Monitoring & Compliance

### Metrics to Track

- **Version adoption**: % of requests per API version
- **Deprecated endpoint usage**: Calls to deprecated endpoints
- **Migration progress**: Active clients on old versions

### Alerts

- **30 days before sunset**: Alert if >10% of traffic still on deprecated version
- **Unexpected v1 traffic after sunset**: Alert operations team

### Quarterly Review

API Team reviews:
- Compliance with versioning policy
- Client feedback on migration process
- Lessons learned from recent deprecations

---

## References

- **Constitutional Principle VII**: API Versioning and Stability
- **Research Document**: `specs/master/research.md` (Section 3: API Versioning Implementation Plan)
- **OpenAPI Contracts**: `specs/master/contracts/`
- **ASP.NET Versioning Docs**: https://github.com/dotnet/aspnet-api-versioning

---

## Changelog

| Version | Date       | Changes                           |
|---------|------------|-----------------------------------|
| 1.0.0   | 2025-12-01 | Initial API versioning policy     |

---

**Document Owner**: API Team
**Review Cycle**: Quarterly
**Next Review**: 2026-03-01