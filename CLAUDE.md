# CLAUDE.md

This file provides repository-specific guidance for Claude Code and other automated
development agents working on DeepLynx Nexus. See the
[development code style guide](documentation/development-code-style-guide.md) for
detailed conventions, including the canonical
[How to Add a New API Version](documentation/development-code-style-guide.md#how-to-add-a-new-api-version)
procedure.

## Architecture

DeepLynx Nexus is a multi-service monorepo. The primary API follows a layered
architecture:

```text
deeplynx.api          → HTTP controllers, middleware, API startup, OpenAPI/Scalar
deeplynx.business     → Domain logic and orchestration
deeplynx.interfaces   → Business-layer interfaces
deeplynx.models       → Request and response DTOs
deeplynx.datalayer    → Entity Framework contexts, entities, and migrations
deeplynx.helpers      → Cross-cutting middleware, exception handlers, and utilities
```

Controllers are thin HTTP adapters. They must not access the database or contain
business rules. Business logic belongs in `deeplynx.business` behind interfaces
from `deeplynx.interfaces`.

### Request Flow

A typical API request follows this path:

```text
HTTP request
    ↓
ASP.NET Core middleware
    ├─ User context, authentication, authorization, and sensitivity checks
    └─ Global exception-handling boundary around downstream processing
    ↓
Controller
    ↓
Business layer
    ↓
DeeplynxContext (EF Core)
    ↓
Response DTO
```

For v2, exceptions propagate out of controllers to the registered global
exception handlers. Those handlers log and translate exceptions into RFC 7807
`ProblemDetails` responses. Error translation does not happen inside v2
controllers.

Legacy v1 controllers retain their existing controller-level error handling.
Their catches may translate an exception before it reaches the global handlers;
this behavior is part of the frozen v1 contract.

### API Versioning

Nexus uses Asp.Versioning with a URL segment:

```text
/api/v{version}/...
```

The configured default version is v1. Call a specific version by including its
segment in the URL:

```text
/api/v1/organizations/...
/api/v2/organizations/...
```

Use v2 for all forward API development. Although v1 is the configured default,
callers should use an explicit version segment so the intended API contract is
unambiguous.

#### Hard Rules

- **v1 is FROZEN and deprecated.** It remains accessible.
- **Do not modify v1 controllers or actions.** Do not add v1 endpoints, develop
  new behavior in v1, migrate v1 to v2 conventions, or strip `try`/`catch` from
  v1 code.
- **v2 controllers and actions must not use controller-level `try`/`catch` for
  HTTP error translation.** Let exceptions reach the global RFC 7807
  `ProblemDetails` exception handlers.
- Put new endpoints and breaking changes, including changes to error response
  contracts, in v2 or a later API version.
- Do not interpret v1 deprecation as authorization to remove or alter its routes
  or response contracts.

These rules apply to both developer-authored and automated changes. A task that
appears to require changing v1 must be stopped and clarified rather than
silently changing the frozen contract.
