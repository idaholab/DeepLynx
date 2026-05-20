# DeepLynx Nexus Backend Development Style Guide

This guide documents backend development conventions for DeepLynx Nexus. It is written in Markdown so it can be used in GitHub or copied into Confluence.

This guide intentionally excludes frontend conventions.

## Purpose

Use this guide when adding or changing backend API behavior, business logic, dependency injection registrations, data access, validation, error handling, or tests.

The goal is consistency:

- Keep controllers thin.
- Keep business rules in business classes.
- Keep persistence concerns in the datalayer.
- Register dependencies explicitly.
- Return predictable API responses.
- Make new domains easy to test and easy to document.

## Solution Breakdown

The repository is organized as a .NET solution with separate projects for API, business logic, interfaces, models, data access, helpers, tests, API tests, documentation, and MCP tooling.

| Project or folder | Responsibility |
|---|---|
| `deeplynx.api` | ASP.NET Core API host, controllers, API startup configuration, middleware pipeline, OpenAPI/Scalar configuration. |
| `deeplynx.business` | Domain/business logic implementations. Business classes own validation, EF queries, persistence orchestration, event creation, and domain-specific rules. |
| `deeplynx.interfaces` | Interfaces for business-layer services. Controllers depend on these interfaces rather than concrete business classes. |
| `deeplynx.models` | Request DTOs, response DTOs, configuration models, and API-facing data shapes. |
| `deeplynx.datalayer` | Entity Framework contexts, entity models, migrations, database version checks, and migration runner support. |
| `deeplynx.helpers` | Shared middleware, auth helpers, validation helpers, cache helpers, clients, exceptions, SignalR hubs, and cross-cutting utilities. |
| `deeplynx.tests` | .NET integration/unit tests, especially business-layer tests backed by Testcontainers. |
| `deeplynx.apitest` | Python API-level tests. |
| `documentation` | Architecture notes, ADRs, and developer documentation. |
| `deeplynx.mcp` | MCP server and tools. Keep this separate from normal API behavior unless the change explicitly touches MCP. |

## Backend Layering Rules

Keep dependencies flowing in one direction:

1. Controllers call business interfaces.
2. Business classes use EF contexts, helper services, and other business interfaces.
3. Models carry API request and response data.
4. Datalayer entities represent persisted tables and relationships.

Do not put business rules in controllers unless the rule is purely HTTP-specific, such as reading a route parameter, query parameter, current user context, or selecting an HTTP status code.

Do not return EF entities directly from controllers. Map entities to response DTOs.

Do not accept EF entities as API request bodies. Use request DTOs.

## How the API Works

The API is hosted by `deeplynx.api/Program.cs`.

The app applies a base path:

```csharp
PathString basePath = "/api/v1";
app.UsePathBase(basePath);
```

Controller routes are written relative to `/api/v1`. For example:

```csharp
[Route("organizations/{organizationId:long}/projects")]
```

The resulting API path is:

```text
/api/v1/organizations/{organizationId}/projects
```

### API Startup Flow

`Program.cs` is responsible for:

- Configuring request size limits.
- Loading the PostgreSQL connection string.
- Configuring Serilog.
- Configuring CORS.
- Configuring JWT bearer authentication.
- Registering controllers and JSON behavior.
- Registering dependency injection services.
- Configuring Entity Framework contexts.
- Running database version checks and migrations.
- Adding OpenAPI and Scalar documentation.
- Building the HTTP middleware pipeline.

### Middleware Order

Middleware order matters. The current API pipeline is:

```csharp
app.UsePathBase(basePath);
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseMiddleware<UserContextMiddleware>();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<SensitivityMiddleware>();
app.UseAuthorization();
app.MapControllers();
```

Important behavior:

- `UseAuthentication()` validates the JWT.
- `UserContextMiddleware` extracts the bearer token and user identity, then stores request-local values in `UserContextStorage`.
- `AuthMiddleware` evaluates `[Auth]`, `[SysAdmin]`, and `[OrgAdmin]` attributes.
- `SensitivityMiddleware` applies sensitivity-label checks.
- Controllers can read current request context from `UserContextStorage`.

### Controller Shape

Controllers should:

- Be decorated with `[ApiController]`.
- Use `[Authorize]` unless the endpoint is intentionally public.
- Use route constraints for IDs, for example `{organizationId:long}`.
- Inject business interfaces and `ILogger<T>`.
- Use explicit `ActionResult<T>` return types so OpenAPI includes DTO schemas.
- Apply `[Auth]`, `[SysAdmin]`, or `[OrgAdmin]` attributes to protected endpoints.
- Keep route methods small.
- Catch exceptions, log failures, and return an API response.

Example:

```csharp
[ApiController]
[Route("organizations/{organizationId:long}/projects")]
[Authorize]
public class ProjectController : ControllerBase
{
    private readonly IProjectBusiness _projectBusiness;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        IProjectBusiness projectBusiness,
        ILogger<ProjectController> logger)
    {
        _projectBusiness = projectBusiness;
        _logger = logger;
    }

    [HttpGet("{projectId:long}", Name = "api_get_a_project")]
    [Auth("read", "project")]
    public async Task<ActionResult<ProjectResponseDto>> GetProject(
        long organizationId,
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var project = await _projectBusiness.GetProject(organizationId, projectId, hideArchived);
            return Ok(project);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving project {projectId}";
            _logger.LogError(exc, message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}
```

### Controller HTTP Response Best Practices

Controllers should return the most specific HTTP status code that describes the result of the request. Existing endpoints often return `200 OK` for successful create, update, delete, archive, and unarchive operations. Preserve existing behavior unless the change intentionally updates the API contract. For new endpoints, prefer the conventions below.

#### Successful Responses

| Scenario | Preferred response | Use when |
|---|---:|---|
| Read one resource | `200 OK` | The resource exists and is returned in the response body. |
| Read a collection | `200 OK` | The request succeeds, even when the collection is empty. Return an empty array/list rather than `404`. |
| Create resource | `201 Created` | A new resource was created. Include the created response DTO. Use `CreatedAtRoute` when there is a route that can fetch the new resource. |
| Create action without a stable fetch route | `200 OK` | A resource or workflow result is created, but the API does not expose a clean location route. This matches several existing controller patterns. |
| Full update | `200 OK` | The updated resource is returned. |
| Partial update, archive, or unarchive | `200 OK` | The updated resource or a status message is returned. |
| Delete with response message | `200 OK` | The API returns a message such as `{ message = "Deleted project 123" }`. |
| Delete without response body | `204 No Content` | The delete succeeds and there is nothing useful to return. |
| Long-running operation accepted | `202 Accepted` | Work has started but is not complete, such as background extraction, ingestion, or queued processing. Include a status ID or polling location when available. |
| File download or stream | `200 OK` | The file exists and the response contains the stream with appropriate content type and length where possible. |

Example create response with a route:

```csharp
return CreatedAtRoute(
    "api_get_a_project",
    new { organizationId, projectId = project.Id },
    project);
```

Example empty collection response:

```csharp
var projects = await _projectBusiness.GetAllProjects(currentUserId, organizationId);
return Ok(projects);
```

#### Client Error Responses

| Scenario | Preferred response | Use when |
|---|---:|---|
| Invalid body, query, or route value | `400 Bad Request` | The request cannot be processed because caller-provided input is invalid. |
| Missing or invalid authentication | `401 Unauthorized` | The caller is not authenticated. Middleware usually handles this. |
| Authenticated but not allowed | `403 Forbidden` | The caller is authenticated but lacks the required role, permission, or admin status. Middleware usually handles this. |
| Resource not found | `404 Not Found` | The requested entity does not exist, belongs to a different scope, or is intentionally hidden by archive filtering. |
| Wrong HTTP method | `405 Method Not Allowed` | ASP.NET Core usually handles this automatically when routes are configured correctly. |
| Conflict with current state | `409 Conflict` | The request is valid, but cannot be completed because of current server state, such as dependent data, duplicate unique values, or state transitions that are not allowed. |
| Unsupported media type | `415 Unsupported Media Type` | The request content type is not supported. ASP.NET Core usually handles this for body binding. |
| Validation shape is correct but semantic validation fails | `400 Bad Request` or `409 Conflict` | Use `400` for invalid input. Use `409` when the input is valid but conflicts with existing state. |

#### Server and Dependency Error Responses

| Scenario | Preferred response | Use when |
|---|---:|---|
| Unexpected server failure | `500 Internal Server Error` | An unhandled or unexpected backend failure occurred. Log details and return a safe message. |
| Upstream service failed | `502 Bad Gateway` | A dependency such as Insight or another service failed or returned an unusable response. |
| Upstream service unavailable | `503 Service Unavailable` | A required dependency is temporarily unavailable and the request may succeed later. |
| Upstream timeout | `504 Gateway Timeout` | A dependency did not respond in time. |

#### Response Body Rules

- Return DTOs for successful resource reads and mutations.
- Return an empty list for successful collection reads with no results.
- Return small `{ message = "..." }` objects only for command-style actions where no DTO is useful.
- Return client-safe error messages. Do not return stack traces or raw exception details.
- Keep response shape consistent within the same controller or domain.
- Do not return `200 OK` for failed operations.
- Do not return `404 Not Found` for an empty collection unless the collection's parent resource is missing.
- Do not return `500 Internal Server Error` for known validation, not-found, authorization, or conflict cases.

### Route Naming

Use descriptive route names with the `api_` prefix:

```csharp
[HttpPost(Name = "api_create_a_project")]
```

Use names that describe the operation and resource. Avoid vague names such as `api_submit`, `api_update`, or `api_get`.

### Route Scopes

Most resources are scoped at one of these levels:

| Scope | Route pattern |
|---|---|
| Organization | `organizations/{organizationId:long}/...` |
| Project | `organizations/{organizationId:long}/projects/{projectId:long}/...` |
| User/global | Resource-specific routes with `[Auth(..., AllowWithoutContext: true)]` when organization or project context is not required. |

When a route needs authorization based on organization or project membership, include the relevant route IDs so `AuthMiddleware` can evaluate permissions.

#### When to Put `projectId` in the Route

Use `projectId` in the route when the operation is scoped to one specific project or when project-level authorization is required.

Use this route shape:

```csharp
[Route("organizations/{organizationId:long}/projects/{projectId:long}/records")]
```

Use route `projectId` for:

- Reading, creating, updating, deleting, archiving, or unarchiving a resource that belongs to exactly one project.
- Endpoints protected by project-level permissions, such as `[Auth("read", "record")]` or `[Auth("write", "data_source")]`.
- Endpoints where the same resource ID could exist or be interpreted differently across projects.
- Endpoints where `AuthMiddleware` must verify project membership or project role permissions.
- Endpoints where `UserContextStorage.OrganizationId` should be derived from the project context.

Examples:

```csharp
[HttpGet("{recordId:long}", Name = "api_get_record")]
[Auth("read", "record")]
public async Task<ActionResult<RecordResponseDto>> GetRecord(
    long organizationId,
    long projectId,
    long recordId)
```

```csharp
[HttpPost(Name = "api_create_data_source")]
[Auth("write", "data_source")]
public async Task<ActionResult<DataSourceResponseDto>> CreateDataSource(
    long organizationId,
    long projectId,
    [FromBody] CreateDataSourceRequestDto dto)
```

Do not put `projectId` in the route when:

- The resource is organization-scoped and may not belong to a project.
- The endpoint lists or searches across multiple projects.
- The endpoint acts on project membership itself under `organizations/{organizationId:long}/projects`.
- The endpoint is user/global and intentionally uses `AllowWithoutContext: true`.

For multi-project operations, use query parameters such as `projectIds` so `AuthMiddleware` can validate every requested project:

```csharp
[HttpGet("search", Name = "api_search_records")]
[Auth("read", "record")]
public async Task<ActionResult<IEnumerable<RecordResponseDto>>> SearchRecords(
    long organizationId,
    [FromQuery] List<long> projectIds)
```

General rule: if the endpoint cannot run correctly without a single project context, put `projectId` in the route. If the endpoint intentionally spans projects, keep project IDs in query parameters and validate all of them.

### Request and Response DTOs

Each domain should expose request and response DTOs from `deeplynx.models`.

Use request DTOs for user input:

```csharp
public async Task<ActionResult<ProjectResponseDto>> CreateProject(
    long organizationId,
    [FromBody] CreateProjectRequestDto dto)
```

Use response DTOs for output:

```csharp
public async Task<ActionResult<ProjectResponseDto>> GetProject(...)
```

DTO conventions:

- Request DTO names should describe the operation, such as `CreateProjectRequestDto` or `UpdateProjectRequestDto`.
- Response DTO names should describe the returned resource, such as `ProjectResponseDto`.
- Use nullable properties on update DTOs when callers may update only part of a resource.
- Add data annotation validation attributes when the business layer should enforce them through `ValidationHelper.ValidateModel(dto)`.
- Do not expose sensitive fields in response DTOs.
- Keep API DTOs separate from EF entities, even when fields currently match.

### API Compatibility

Treat controller routes, request DTOs, response DTOs, status codes, and auth requirements as API contracts.

Compatibility rules:

- Do not remove or rename public routes without a coordinated API change.
- Do not remove response fields that clients may depend on.
- Do not change field meaning without updating documentation and tests.
- Prefer adding optional request fields over changing required request fields.
- Prefer adding nullable response fields over changing existing response shapes.
- Preserve existing success status codes unless the ticket explicitly changes the API contract.
- If a breaking change is required, call it out in the PR description and update API documentation.

### Query Parameters, Filtering, and Pagination

Use query parameters for optional filters, pagination, sorting, and cross-resource search inputs.

Query parameter conventions:

- Use route parameters for required resource identity, such as `organizationId`, `projectId`, or `recordId`.
- Use query parameters for optional behavior, such as `hideArchived`, `archive`, `page`, `pageSize`, `limit`, `rowStride`, or `projectIds`.
- Give optional query parameters safe defaults.
- Validate numeric query parameters before using them in EF queries.
- Enforce maximum page sizes or limits for endpoints that can return large datasets.
- Use `400 Bad Request` when query parameters are out of range.
- Keep names consistent with existing endpoints, especially `hideArchived`, `page`, `pageSize`, and `projectIds`.

Use `PaginatedResponse<T>` for endpoints that return large or user-driven result sets:

```csharp
public async Task<ActionResult<PaginatedResponse<EventResponseDto>>> QueryEvents(...)
```

Pagination rules:

- Page numbers should start at `1`.
- Page size should be greater than `0`.
- Page size should have an upper bound.
- Return an empty `Items` list when the page is valid but no records match.
- Include `TotalCount` when callers need to render paging controls.

### OpenAPI and Scalar

OpenAPI is configured in `Program.cs` with:

```csharp
builder.Services.AddOpenApi(...)
app.MapOpenApi();
app.MapScalarApiReference(...);
```

When adding endpoints:

- Use explicit generic return types such as `ActionResult<ClassResponseDto>`.
- Use XML comments on controller actions and parameters.
- Add meaningful route names.
- Make non-nullable query parameters intentional; OpenAPI marks them as required.
- Use existing tags and tag-group conventions when adding new OpenAPI metadata.

## Authentication, Authorization, and User Context

Authentication uses JWT bearer configuration in `Program.cs`.

Authorization is mostly handled through custom attributes and middleware:

```csharp
[Auth("read", "project")]
[Auth("update", "user")]
[SysAdmin]
[OrgAdmin]
```

### Auth Attribute

`[Auth(action, resource)]` checks whether the current user has a role permission for the resource in the current organization or project context.

Common actions:

- `read`
- `write`
- `update`
- `delete`
- `archive`
- `unarchive`

Common resources match domain names such as:

- `organization`
- `project`
- `record`
- `edge`
- `relationship`
- `class`
- `data_source`
- `tag`
- `permission`
- `role`

Use `AllowWithoutContext: true` only when the endpoint is intentionally available without an organization or project route context.

Use `includeArchived: true` only when the endpoint must operate on archived records, such as archive/unarchive operations.

### UserContextStorage

`UserContextStorage` stores request-local values using `AsyncLocal`. Controllers and business classes may read:

- `UserContextStorage.UserId`
- `UserContextStorage.Email`
- `UserContextStorage.Token`
- `UserContextStorage.OrganizationId`
- `UserContextStorage.IsSysAdmin`
- `UserContextStorage.IsOrgAdmin`
- `UserContextStorage.IsProjectAdmin`

Prefer passing important IDs explicitly from controllers to business methods. Use `UserContextStorage` for request context that is genuinely cross-cutting or already established by middleware.

## Dependency Injection

Dependency injection registrations live in `deeplynx.api/Program.cs`.

Current common lifetimes:

| Registration | Lifetime | Notes |
|---|---|---|
| `DeeplynxContext` | Transient | Main EF context. Configured with Npgsql and pgvector support. |
| `StagingContext` | Transient | Staging EF context. |
| Business interfaces | Transient | Most `I...Business` services are transient. |
| `IBulkCopyUpsertExecutor` | Scoped | Bulk database operation helper. |
| `ISensitivityLabelService` | Scoped | Sensitivity-label service. |
| Typed HTTP clients | Managed by `AddHttpClient` | Example: `InsightServiceClient`. |

### DI Rules

When adding a new business domain:

1. Add an interface in `deeplynx.interfaces`.
2. Add the implementation in `deeplynx.business`.
3. Inject dependencies through the constructor.
4. Register the interface and implementation in `Program.cs`.
5. Inject the interface into controllers or other business classes.
6. Add tests for the business class.

Example registration:

```csharp
builder.Services.AddTransient<IExampleBusiness, ExampleBusiness>();
```

Example implementation:

```csharp
public class ExampleBusiness : IExampleBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IEventBusiness _eventBusiness;
    private readonly ILogger<ExampleBusiness> _logger;

    public ExampleBusiness(
        DeeplynxContext context,
        IEventBusiness eventBusiness,
        ILogger<ExampleBusiness> logger)
    {
        _context = context;
        _eventBusiness = eventBusiness;
        _logger = logger;
    }
}
```

Do not manually instantiate business classes inside controllers. Use constructor injection.

Do not resolve services from `IServiceProvider` unless there is a specific framework integration reason.

Do not introduce static mutable service state. If state is request-local, use middleware/context patterns. If state is shared, use a cache or persistent store with clear lifecycle rules.

## Business Layer Style

Business classes should:

- Implement a matching interface from `deeplynx.interfaces`.
- Use async EF Core APIs.
- Validate request DTOs before persisting.
- Throw domain-appropriate exceptions when an operation cannot be completed.
- Map EF entities to response DTOs.
- Save changes deliberately.
- Create events where the domain already tracks create/update/delete/archive behavior.
- Keep controller-specific HTTP behavior out of business logic.

Example business method shape:

```csharp
public async Task<ProjectResponseDto> CreateProject(
    long currentUserId,
    long organizationId,
    CreateProjectRequestDto dto)
{
    ValidationHelper.ValidateModel(dto);

    var project = new Project
    {
        Name = dto.Name,
        Description = dto.Description,
        OrganizationId = organizationId,
        LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
        LastUpdatedBy = currentUserId
    };

    _context.Projects.Add(project);
    await _context.SaveChangesAsync();

    return new ProjectResponseDto
    {
        Id = project.Id,
        Name = project.Name,
        Description = project.Description,
        OrganizationId = project.OrganizationId,
        LastUpdatedAt = project.LastUpdatedAt,
        LastUpdatedBy = project.LastUpdatedBy
    };
}
```

### DateTime Convention

Existing code stores timestamps using UTC values with unspecified kind:

```csharp
DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
```

Follow the existing convention for persisted `LastUpdatedAt` style fields unless the database model changes.

### Archive and Delete Behavior

Many domains support archive/unarchive behavior. Follow existing domain conventions before adding hard deletes.

Archive/delete rules:

- Default list and read endpoints should usually hide archived records through `hideArchived = true`.
- Archive and unarchive endpoints should use `includeArchived: true` in `[Auth]` when archived records must be found.
- Archive operations should update `LastUpdatedAt` and `LastUpdatedBy` when the entity supports those fields.
- Hard delete only when the domain already supports it or the ticket explicitly requires it.
- Before hard delete, check dependent data and throw `DependencyDeletionException` or another clear conflict exception when deletion would break relationships.
- Prefer `409 Conflict` when a delete cannot be completed because dependent records exist.
- Add tests for archived reads, unarchived reads, archive operations, and dependency-blocked deletes.

### Events and Audit Trail

Many business classes create events for create, update, delete, archive, and unarchive operations through `IEventBusiness`.

Event rules:

- Create events for user-visible mutations when the domain already participates in event tracking.
- Use consistent operation names such as `create`, `update`, `delete`, `archive`, and `unarchive`.
- Use consistent entity types from `ValidationHelper.AllowedEntityTypes`.
- Include `organizationId`, `projectId` when applicable, entity ID, entity name, and useful non-sensitive properties.
- For bulk operations, prefer the existing bulk event pattern rather than creating excessive per-row noise.
- Do not put secrets, tokens, or large payloads in event properties.
- Add or update tests when event creation is part of the expected business behavior.

## Error Handling

Error handling should make failures predictable for API consumers, useful for developers, and safe for production logs.

The backend uses three layers of error behavior:

1. Middleware handles authentication, authorization, user context, sensitivity checks, and request pipeline failures.
2. Controllers translate business exceptions into HTTP responses.
3. Business classes validate inputs and throw meaningful exceptions when domain operations cannot be completed.

### Current Controller Pattern

Most controllers currently wrap route logic in `try`/`catch`, log errors, and return a response:

```csharp
try
{
    var result = await _business.DoWork(...);
    return Ok(result);
}
catch (Exception exc)
{
    var message = "An error occurred while doing work";
    _logger.LogError(exc, message);
    return StatusCode(StatusCodes.Status500InternalServerError, message);
}
```

Business classes throw exceptions for invalid domain states, missing records, validation failures, dependency conflicts, and failed operations.

When touching an existing endpoint, preserve behavior unless the ticket includes an error-handling change. For new endpoints, use the preferred mapping below.

### Preferred Controller Mapping

For new or touched endpoints, map known exception types to specific status codes before falling back to `500`.

| Exception or condition | Recommended status | Notes |
|---|---:|---|
| `ValidationException` | `400 Bad Request` | Request body fails data annotation or long ID validation. Message is passed through to the client unsanitized — keep it client-safe. |
| `ArgumentException` | `400 Bad Request` | Invalid query/body values or unsupported operation/entity type. **Currently routed to `500` by `BadRequestExceptionHandler`** pending the throw-site audit (see `BadRequestExceptionHandler` remarks); raw message is sanitized in non-Development environments. |
| `InvalidRequestException` | `400 Bad Request` | Domain request is malformed or unsupported. Message is passed through to the client unsanitized — keep it client-safe (no env-var names, file paths, SQL fragments, internal IDs). |
| `KeyNotFoundException` | `404 Not Found` | Requested entity does not exist or is hidden by archive filtering. |
| `NoResultsException` | `404 Not Found` | Query succeeded but no result exists when one is required. |
| `DependencyDeletionException` | `409 Conflict` | Delete is blocked by dependent records. |
| `InvalidOperationException` | `409 Conflict` or `400 Bad Request` | Choose based on whether current server state conflicts with the operation. |
| External service failure | `502 Bad Gateway` | The API is healthy but an upstream dependency failed. |
| Unexpected `Exception` | `500 Internal Server Error` | Log the exception object and return a sanitized message. |

### Preferred Controller Template

Use this pattern for new controller actions. Keep the success path short, catch expected exceptions first, and put the generic catch last.

```csharp
try
{
    var currentUserId = UserContextStorage.UserId;
    var result = await _projectBusiness.UpdateProject(
        currentUserId,
        organizationId,
        projectId,
        dto);

    return Ok(result);
}
catch (ValidationException exc)
{
    _logger.LogWarning(
        exc,
        "Invalid update project request for project {ProjectId} in organization {OrganizationId}",
        projectId,
        organizationId);

    return BadRequest(exc.Message);
}
catch (KeyNotFoundException exc)
{
    _logger.LogWarning(
        exc,
        "Project {ProjectId} was not found in organization {OrganizationId}",
        projectId,
        organizationId);

    return NotFound(exc.Message);
}
catch (DependencyDeletionException exc)
{
    _logger.LogWarning(
        exc,
        "Project {ProjectId} could not be deleted because dependencies exist",
        projectId);

    return Conflict(exc.Message);
}
catch (Exception exc)
{
    _logger.LogError(
        exc,
        "Unexpected error updating project {ProjectId} in organization {OrganizationId}",
        projectId,
        organizationId);

    return StatusCode(
        StatusCodes.Status500InternalServerError,
        "An unexpected error occurred while updating the project");
}
```

### Business Layer Error Rules

Business classes should throw exceptions rather than returning ambiguous failure values.

Use these patterns:

- Throw `ValidationException` when DTO validation fails.
- Throw `ArgumentException` when a method argument has an unsupported value.
- Throw `KeyNotFoundException` when a required entity does not exist or is excluded by archive filtering.
- Throw `InvalidOperationException` when the current database state prevents a valid request from completing.
- Throw `DependencyDeletionException` when a delete is blocked by dependent data.
- Return an empty collection only when "no results" is a valid successful response.
- Do not catch and hide exceptions unless the business method can fully recover.

Example:

```csharp
var project = await _context.Projects
    .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == organizationId);

if (project == null)
    throw new KeyNotFoundException($"Project with id {projectId} does not exist");

if (project.IsArchived && hideArchived)
    throw new KeyNotFoundException($"Project with id {projectId} is archived");
```

### External Service Errors

When calling an external service, separate upstream failures from internal failures.

- Return `502 Bad Gateway` when the upstream service responds with an error or cannot be reached.
- Return `504 Gateway Timeout` if timeout handling is added and the upstream service times out.
- Log enough context to identify the upstream dependency and operation.
- Do not return upstream secrets, internal URLs, bearer tokens, or raw response bodies that may contain sensitive data.

Example:

```csharp
catch (HttpRequestException exc)
{
    _logger.LogError(exc, "Insight service request failed while embedding record {RecordId}", recordId);
    return StatusCode(StatusCodes.Status502BadGateway, "Insight service request failed");
}
```

### Middleware Errors

Middleware may short-circuit requests for authentication, authorization, context, and sensitivity failures.

Expected middleware responses:

| Condition | Status |
|---|---:|
| Missing or invalid authenticated user | `401 Unauthorized` |
| Authenticated user lacks required role permission | `403 Forbidden` |
| Required organization/project context is missing | `400 Bad Request` |
| Required organization admin or system admin access is missing | `403 Forbidden` |

Middleware should return small JSON error objects and avoid leaking internal implementation details.

### Error Handling Rules

- Log the exception object, not only the interpolated string.
- Return client-safe messages. Avoid returning stack traces or full exception details in new code.
- Include useful resource IDs in logs.
- Catch specific exceptions before generic exceptions.
- Use `LogWarning` for expected client or domain errors.
- Use `LogError` for unexpected server errors or failed dependencies.
- Do not swallow exceptions in business classes.
- Do not return `null` to mean failure; throw a meaningful exception or return an explicit empty result when empty is valid.
- Keep validation errors deterministic and easy to test.
- Do not use exceptions for normal branching when a simple conditional is clearer.
- Keep API error responses consistent within the controller or domain being changed.

## Validation

Use `ValidationHelper.ValidateModel(dto)` in business methods for create/update operations that accept DTOs.

`ValidationHelper` applies data annotation validation and validates non-nullable `long` properties are greater than zero.

Use `ValidationHelper.ValidateTypes(value, "EntityType")` and `ValidationHelper.ValidateTypes(value, "Operation")` when accepting event-like entity types or operations.

Validation belongs in the business layer unless it is strictly HTTP binding validation.

## Data Access and Entity Framework

Use EF Core through `DeeplynxContext` or `StagingContext`.

Data access conventions:

- Prefer async methods such as `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, and `SaveChangesAsync`.
- Filter archived records by default where the domain supports archive behavior.
- Use `Include` only when navigation data is needed.
- Use projection to DTOs for read-heavy endpoints.
- Use transactions when multiple saves must succeed or fail as a unit.
- Clear the EF change tracker after stored procedure calls that mutate tracked data.
- Avoid materializing large tables before filtering.
- Apply `Where`, `OrderBy`, `Skip`, and `Take` before `ToListAsync`.
- Use `AnyAsync` for existence checks instead of loading entities when the entity data is not needed.
- Prefer `Select` projections for read endpoints that do not need tracked entities.
- Consider `AsNoTracking` for read-only queries when tracking is not needed.
- Avoid N+1 query patterns inside loops.
- Add indexes for new query patterns that filter or join on large tables.

Stored procedure test convention from `CONTRIBUTING.md`:

```csharp
Context.ChangeTracker.Clear();
```

## Configuration and Secrets

Configuration comes from app settings, environment variables, Docker Compose, and deployment configuration.

Configuration rules:

- Do not hardcode secrets, tokens, passwords, connection strings, or environment-specific URLs.
- Keep local-only values in `.env`.
- When adding or changing environment variables, update `.env_sample`, Docker Compose, Kubernetes manifests, and GitHub Actions configuration as applicable.
- Validate required environment variables at startup and fail fast with a clear message.
- Do not log full configuration values when they may contain secrets.
- Prefer typed or centralized configuration helpers when a setting is used in multiple places.
- Document new required settings in `README.md` or developer docs when setup changes.

## Migrations

Database changes belong in `deeplynx.datalayer`.

Create a migration:

```bash
dotnet ef migrations add <MIGRATION_NAME> -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

Update the database:

```bash
dotnet ef database update -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

Migration rules:

- Use descriptive migration names.
- Review generated migrations before committing.
- Keep entity model, context configuration, and migration aligned.
- If adding environment variables, update `.env_sample` and GitHub Actions configuration.
- Add or update tests for behavior that depends on the schema change.

## Logging

The API uses Serilog and ASP.NET Core `ILogger<T>`.

Controller and business classes should receive `ILogger<T>` through DI when they need logging.

Serilog is configured in `Program.cs` to write to console and PostgreSQL. Logs are operational data, so treat them as production-facing artifacts.

Logging conventions:

- Use structured logging for IDs and important values.
- Use `LogWarning` for expected client/domain failures.
- Use `LogError` for unexpected failures.
- Use `LogInformation` for meaningful lifecycle events, not for noisy per-record traces.
- Use `LogDebug` only when debug-level logging is intentionally enabled and the message is useful during troubleshooting.
- Do not log secrets, bearer tokens, API keys, or sensitive payloads.
- Avoid noisy information logs inside high-volume loops.
- Prefer message templates over string interpolation.
- Include exception objects when logging exceptions.
- Include resource identifiers, organization IDs, project IDs, user IDs, and operation names when helpful.
- Avoid logging full DTOs unless every field is known to be non-sensitive.

### Logging Levels

| Level | Use for |
|---|---|
| `LogTrace` | Very detailed temporary diagnostics. Avoid in normal application code. |
| `LogDebug` | Developer diagnostics that are safe but too noisy for normal operations. |
| `LogInformation` | Application lifecycle, startup, migrations, successful major background operations. |
| `LogWarning` | Expected failures such as validation errors, not-found cases, blocked deletes, or denied domain operations. |
| `LogError` | Unexpected exceptions, failed dependencies, failed persistence operations, and unrecoverable request failures. |
| `LogCritical` | Application-level failures that require immediate attention or cause shutdown. |

### Structured Logging

Prefer structured logs:

```csharp
_logger.LogError(
    exc,
    "Failed to update project {ProjectId} in organization {OrganizationId}",
    projectId,
    organizationId);
```

Avoid interpolated logs:

```csharp
_logger.LogError($"Failed to update project {projectId} in organization {organizationId}: {exc}");
```

Structured logs preserve searchable fields and keep exception details attached to the log event.

### What to Log

Good log context:

- Operation name, such as `CreateProject`, `ArchiveOrganization`, or `UploadFile`.
- Relevant resource IDs.
- Current user ID when it helps investigate authorization or ownership.
- External dependency name when calling another service.
- Archive flag, pagination, or query mode when it materially changes behavior.

Example:

```csharp
_logger.LogWarning(
    exc,
    "User {UserId} could not archive project {ProjectId} in organization {OrganizationId}",
    currentUserId,
    projectId,
    organizationId);
```

### What Not to Log

Do not log:

- Bearer tokens.
- API keys.
- Passwords.
- Raw authorization headers.
- OAuth secrets or authorization codes.
- Full request bodies that may contain user content, labels, extracted text, files, or metadata.
- Connection strings.
- Personally sensitive data beyond what is already necessary for operations.

When in doubt, log IDs and operation names rather than payloads.

### Logging in Business Classes

Business classes do not need to log every expected exception if the controller will log it. Add business-layer logs when:

- The method performs a multi-step operation and an intermediate step is important.
- A background, event, or notification operation fails outside a normal controller action.
- The method calls an external service or low-level helper where context may be lost.
- The operation changes many records or performs a bulk action.

Avoid double-logging the same exception in both business and controller layers unless each log adds materially different context.

### Logging in Middleware

Middleware logs should be concise because middleware runs on every request.

- Use warnings for denied access only when the event is useful for investigation.
- Avoid logging all claims or full headers in production-facing code.
- Never log bearer tokens.
- Clear request-local context after each request, as `UserContextMiddleware` already does.

## Testing

Business logic should be tested in `deeplynx.tests`.

Use the shared integration test fixture:

```csharp
[Collection("Test Suite Collection")]
public class ExampleBusinessTests : IntegrationTestBase
{
    public ExampleBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }
}
```

Testing expectations:

- Add tests for new business methods.
- Add tests for validation failures.
- Add tests for not-found and archived-resource behavior.
- Add tests for permission-sensitive logic when business behavior changes based on user role or context.
- Add tests for database changes.
- Use mocks for external services, SignalR hubs, or expensive dependencies.
- Run `dotnet test` before submitting backend changes.

Controller changes should also be manually tested through Scalar, Postman, or API-level tests when adding or changing routes.

## Adding a New Backend Domain

Use this checklist when adding a new backend resource.

1. Add EF entity changes in `deeplynx.datalayer/Models` if persistence is needed.
2. Update `DeeplynxContext` relationships and `DbSet` properties.
3. Create an EF migration.
4. Add request and response DTOs in `deeplynx.models`.
5. Add an interface in `deeplynx.interfaces`.
6. Add a business implementation in `deeplynx.business`.
7. Register the business implementation in `Program.cs`.
8. Add a controller in `deeplynx.api/Controllers`.
9. Add `[Authorize]` and the correct `[Auth]`, `[SysAdmin]`, or `[OrgAdmin]` attributes.
10. Use explicit `ActionResult<T>` types.
11. Add XML comments for controller methods.
12. Add business tests in `deeplynx.tests`.
13. Run `dotnet test`.
14. Manually verify new routes in Scalar or another API client.

## Backend Code Style

General C# conventions:

- Follow standard .NET naming conventions.
- Use `PascalCase` for public types, methods, and properties.
- Use `_camelCase` for private fields.
- Prefer `var` when the type is obvious from the right-hand side.
- Use expression clarity over cleverness.
- Keep methods focused and readable.
- Avoid unrelated refactors while implementing feature work.
- Add comments only when they clarify non-obvious behavior.

Controller conventions:

- Keep controller methods thin.
- Avoid direct EF access from controllers.
- Do not manually construct business dependencies.
- Use route constraints for numeric IDs.
- Use `[FromBody]` and `[FromQuery]` explicitly when helpful.

Business conventions:

- Validate DTOs near the start of create/update methods.
- Throw meaningful exceptions.
- Prefer small helper methods for repeated mapping or domain checks.
- Keep cross-domain calls explicit through injected interfaces.

DTO conventions:

- Keep DTOs small and purpose-specific.
- Do not leak persistence-only fields.
- Use nullable properties intentionally.
- Use validation attributes for required input rules.

## Pull Request Expectations

Backend pull requests should include:

- A clear description of the behavior change.
- A focused scope that matches the ticket or stated purpose of the PR.
- Business-layer tests for new logic.
- Migration files for schema changes.
- Updated documentation when behavior or setup changes.
- Manual API verification for new or changed endpoints.
- No frontend changes unless the ticket explicitly includes frontend scope.

Keep pull requests lean:

- Prefer small, reviewable PRs over large PRs that mix unrelated work.
- Split independent features, bug fixes, schema changes, and refactors into separate PRs when practical.
- Avoid drive-by refactors, formatting churn, or unrelated cleanup in feature PRs.
- If a large change is unavoidable, organize it into clear commits or sections and explain why it could not be split.
- Keep generated files, migrations, and mechanical changes easy to identify in the PR description.

Before opening a PR:

```bash
dotnet test
```

If API routes were added or changed, run the API and verify the routes in Scalar.
