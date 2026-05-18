# DeepLynx Nexus — Backend Architecture

## Overview

DeepLynx Nexus uses a **Classic Layered Architecture (N-Tier)**. The codebase is organized into horizontal layers, each with a distinct responsibility. Dependencies flow strictly inward — outer layers depend on inner layers, and this is enforced at the project reference level in the `.sln` file rather than by convention alone.

This document describes the architecture as-implemented, explains the role of each layer, and addresses why other common patterns (Clean Architecture, Hexagonal, Vertical Slice, CQRS/DDD) do not apply.

---

## Project Structure and Dependency Graph

The solution is organized into distinct .NET projects, one per layer plus shared concerns:

| Project | Role |
|---------|------|
| `deeplynx.api` | ASP.NET Core controllers and the middleware pipeline (`Program.cs`) |
| `deeplynx.business` | All business logic — one `*Business.cs` class per domain concept |
| `deeplynx.interfaces` | `I*Business` interfaces that define contracts between layers |
| `deeplynx.models` | Request/response DTOs — data shapes with no behavior |
| `deeplynx.datalayer` | EF Core `DeeplynxContext`, entity models, and migrations |
| `deeplynx.helpers` | Middleware, caching, authorization services, and utilities |
| `deeplynx.tests` | xUnit integration tests using Testcontainers |

### Enforced Dependency Direction

The `.csproj` project references enforce the following dependency graph. No circular references exist.

```
deeplynx.api
  └─→ deeplynx.business
  └─→ deeplynx.interfaces
  └─→ deeplynx.helpers
  └─→ deeplynx.models
  └─→ deeplynx.datalayer

deeplynx.business
  └─→ deeplynx.interfaces
  └─→ deeplynx.models
  └─→ deeplynx.datalayer
  └─→ deeplynx.helpers

deeplynx.interfaces
  └─→ deeplynx.models
  └─→ deeplynx.datalayer

deeplynx.helpers
  └─→ deeplynx.interfaces
  └─→ deeplynx.models
  └─→ deeplynx.datalayer

deeplynx.datalayer     →  (no internal dependencies)
deeplynx.models        →  (no internal dependencies)
```

---

## Request Lifecycle

A typical API request flows as follows:

```
HTTP Request
    ↓
UserContextMiddleware       — extracts JWT, populates UserContextStorage (AsyncLocal)
    ↓
AuthMiddleware              — reads [Auth]/[OrgAdmin]/[SysAdmin] attributes, checks RBAC
    ↓
SensitivityMiddleware       — applies sensitivity label filtering
    ↓
Controller                  — routes to method, calls I*Business via DI
    ↓
*Business                   — domain logic, EF Core queries, inter-service orchestration
    ↓
DeeplynxContext (EF Core)   — database access
    ↓
Response DTO                — returned up the chain
```

No component bypasses this chain. Controllers do not touch the database; business classes do not set HTTP response codes.

---

## Layer Detail

### Presentation Layer — Controllers (`deeplynx.api`)

Controllers are **thin HTTP adapters**. They are responsible for:

- Routing and HTTP method binding
- Reading `UserContextStorage` for the current `OrganizationId` and `UserId`
- Calling the injected `I*Business` interface
- Returning the appropriate HTTP status code
- Catching and logging unexpected exceptions

Controllers contain no conditional business logic, no data access, and no validation beyond what the framework provides via model binding. Every method follows the same shape:

```csharp
[HttpGet]
[Auth("read", "class")]
public async Task<ActionResult<IEnumerable<ClassResponseDto>>> GetAllClasses(long projectId)
{
    try
    {
        var organizationId = UserContextStorage.OrganizationId;
        var classes = await _classBusiness.GetAllClasses(organizationId, [projectId], true);
        return Ok(classes);
    }
    catch (Exception exc)
    {
        _logger.LogError(exc.Message);
        return StatusCode(500, exc.Message);
    }
}
```

### Business Layer — Business Classes (`deeplynx.business`)

The business layer is the thickest layer. It owns:

- All domain rules and validation
- EF Core queries (directly against `DeeplynxContext` — no repository intermediary)
- Orchestration across related domains (e.g., `ClassBusiness` calls `EventBusiness` to log audit events)
- Explicit database transactions for multi-step operations
- Stored procedure calls for complex batch operations

Each domain concept has one `*Business.cs` class (e.g., `ClassBusiness`, `EdgeBusiness`, `RecordBusiness`) that handles all operations for that concept. The class is registered in DI via its `I*Business` interface with a transient lifetime.

**Inter-business dependencies** are common. For example:

- `ClassBusiness` → `IEventBusiness`, `IRecordBusiness`, `IRelationshipBusiness`
- `RecordBusiness` → `IEventBusiness`, `ITagBusiness`, `ISensitivityLabelBusiness`

This horizontal coupling within the layer is intentional — it centralizes audit logging and keeps related operations consistent — but it means changes to one business class can have cascading effects.

### Interface Layer (`deeplynx.interfaces`)

Every business class has a corresponding `I*Business` interface. Interfaces serve two purposes:

1. **DI registration** — controllers and other business classes depend on interfaces, not concrete types
2. **Testability** — business classes can be mocked in tests

Interfaces are 1:1 with their implementations. They do not represent ports in the Hexagonal sense or use cases in the Clean Architecture sense — they are straightforward contracts for the business operations each class exposes.

### Data Transfer Objects (`deeplynx.models`)

All data crossing layer boundaries uses DTOs:

- **`*RequestDto`** — inbound data from HTTP requests. Contains validation attributes (`[Required]`, `[MaxLength]`) and JSON serialization metadata.
- **`*ResponseDto`** — outbound data returned to the caller. Properties are decorated with `[Column]` attributes to support EF Core raw SQL projection.

DTOs are **anemic** — properties only, no methods or behavior. There is no mapping library (AutoMapper etc.); business classes construct response DTOs manually from entity data.

### Data Layer (`deeplynx.datalayer`)

`DeeplynxContext` (EF Core) is the sole database abstraction. Entity models in this project are also anemic — they are decorated with EF Core attributes (`[Table]`, `[Key]`, `[ForeignKey]`) and contain navigation properties, but no behavior.

There is no repository pattern. Business classes inject and use `DeeplynxContext` directly. EF Core is itself an abstraction over the database; the team's judgment is that wrapping it in a further `IRepository<T>` layer adds overhead without proportional benefit for a CRUD-oriented domain.

`StagingContext` exists as a second context for OLAP/staging data and follows the same pattern.

### Cross-Cutting Concerns (`deeplynx.helpers`)

Rather than pipeline behaviors or decorator chains, cross-cutting concerns are handled through:

- **ASP.NET Core middleware** — `UserContextMiddleware`, `AuthMiddleware`, `SensitivityMiddleware`
- **`UserContextStorage`** — `AsyncLocal<>` storage that makes the current user, organization, and permission flags available anywhere in the request without threading parameters through method signatures
- **Caching** — `ICacheBusiness` abstraction with Redis and in-memory implementations, switchable via environment variable
- **Authorization services** — `IOrgRolePermissionService`, `IProjectRolePermissionService`, `IAdminService` live in helpers and are called by `AuthMiddleware`
- **File storage** — Factory/Strategy pattern: `IFileBusinessFactory` selects between `FileFilesystemBusiness`, `FileAzureBusiness`, and `FileS3Business` based on configuration

---

## Authorization Pattern

Nexus uses **custom authorization attributes** rather than ASP.NET Core's `[Authorize]`:

```csharp
[Auth("read", "record")]       // checks action + resource in the RBAC system
[OrgAdmin]                      // requires organization administrator role
[SysAdmin]                      // requires system administrator role
```

`AuthMiddleware` processes these attributes during the request:

1. Extracts `organizationId` and `projectId` from the route
2. Verifies the organization or project exists (and is not archived, if applicable)
3. Checks the user's role via `IOrgRolePermissionService` or `IProjectRolePermissionService`
4. Returns `403 Forbidden` immediately if the check fails; otherwise calls `_next`

System administrators bypass resource-level permission checks. The checked results (`IsSysAdmin`, `IsOrgAdmin`, `IsProjectAdmin`) are stored in `UserContextStorage` for use downstream.

---

## What This Architecture Is Not

### Not Clean Architecture

Clean Architecture (Robert Martin) places domain entities at the center with zero external dependencies, surrounds them with use-case/interactor objects that define application behavior, and treats all frameworks and databases as outer-ring plugins.

Nexus diverges on several key points:

- There is no **use case / interactor layer**. Business classes combine orchestration, data access, and domain logic in one class rather than separating "what the application does" from "how data is fetched."
- There is no **entity/value object distinction**. Entities in `deeplynx.datalayer` are pure data bags with no behavior. All business rules live in services, not in the domain model.
- The business layer depends on `DeeplynxContext` **directly**, not on an abstraction of the data layer. In Clean Architecture, the domain would define a data gateway interface; infrastructure would implement it. Here, EF Core is treated as the abstraction.
- There are no explicit **input/output boundary objects** (request/response models at use-case boundaries). DTOs flow directly through layers.

### Not Hexagonal Architecture (Ports and Adapters)

Hexagonal Architecture (Alistair Cockburn) defines the application as a hexagon with the domain at the center, driven by inbound ports (interfaces the domain exposes to callers) and driving outbound ports (interfaces the domain uses to reach infrastructure), with adapters implementing those ports.

Nexus diverges because:

- There are no **ports**. `I*Business` interfaces exist, but they are contracts on the business layer for DI and testing — not formally defined boundaries between the domain and its driving/driven actors.
- The business layer is not **isolated from infrastructure**. It calls EF Core directly rather than through a domain-defined data port.
- There is no **adapter pattern**. The file storage factory/strategy pattern (`IFileBusiness` with multiple implementations) is the closest thing to an adapter, but it is not applied uniformly across infrastructure concerns.

### Not Vertical Slice Architecture

Vertical Slice Architecture (Jimmy Bogard) organizes code by **feature**, not by layer. Each "slice" owns everything for one operation — handler, logic, data access, and DTOs — in one cohesive unit. It is typically implemented with MediatR.

Nexus organizes in the opposite direction:

| Dimension | Vertical Slice Architecture | Nexus |
|-----------|----------------------------|-------|
| **Code organization** | `Features/CreateClass/`, `Features/GetClass/` | `deeplynx.api`, `deeplynx.business`, `deeplynx.datalayer` |
| **Operation handler** | `CreateClassHandler : IRequestHandler<CreateClassCommand>` — one handler per operation | `ClassBusiness` — one class for all class operations |
| **Mediator** | MediatR dispatches commands/queries | Controllers call `I*Business` directly via DI |
| **Cross-slice dependencies** | Discouraged; slices self-contained | Expected; business classes call other business classes |
| **CQRS alignment** | Natural fit via separate command/query types | Not present; reads and writes are methods on the same class |
| **Cross-cutting concerns** | MediatR pipeline behaviors | ASP.NET Core middleware and custom attributes |

There is no `Features/` directory, no MediatR dependency, and no command/query object types anywhere in the solution.

### Not CQRS or Domain-Driven Design

- **No CQRS** — read and write operations share the same business class and model. There is no separation of query and command models.
- **No aggregates or value objects** — entities are individual data bags without aggregate root coordination or encapsulated invariants.
- **No domain events** — the `EventBusiness` produces audit log records, not domain state-change events consumed by other parts of the system.

---

## Known Architectural Tensions

These are known tradeoffs, not defects. They are worth understanding when adding features or planning refactors.

| Tension | Description |
|---------|-------------|
| **Fat business classes** | As domain logic grows, combining orchestration, data access, and rules in one class makes individual classes harder to navigate. |
| **Horizontal business coupling** | `ClassBusiness` calling `EventBusiness` and `RecordBusiness` means a signature change in a called class requires updating all callers. |
| **No repository abstraction** | Because business classes use `DeeplynxContext` directly, meaningful tests require a real database. The Testcontainers setup handles this, but test startup time reflects it. |
| **`UserContextStorage` is implicit** | Business methods that require `OrganizationId` or `UserId` read them from `AsyncLocal` storage rather than accepting them as parameters. This is invisible in method signatures. |
| **Anemic domain model** | Business rules cannot be enforced by entities themselves. Nothing prevents creating an entity in an invalid state if the business class is bypassed. |

---

## Architectural Strengths

- **Dependency direction is structurally enforced** — project references make it impossible to introduce cycles at compile time
- **Controllers are pure HTTP adapters** — no business logic leaks into the presentation layer
- **Interface-based DI throughout** — every major component is swappable via configuration
- **Consistent patterns across domains** — all business classes follow the same structure, making navigation predictable
- **Cross-cutting concerns are well-isolated** — auth, caching, and sensitivity labels are handled in middleware/helpers, not scattered through business classes
- **Strategy pattern for infrastructure variability** — file storage and caching implementations are selected at runtime without changing call sites
