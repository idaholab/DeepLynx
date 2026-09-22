# Migrating API Error Handling from v1 to v2

This guide is for applications that consume the DeepLynx Nexus API and are
changing their requests from `/api/v1` to `/api/v2`. It covers the error-response
changes introduced by the v2 global exception handlers. It does not describe
how to implement v2 endpoints or catalog endpoint-specific request and success
response changes.

Before migrating, compare every operation the application uses in the v1 and v2
OpenAPI documents. Those documents remain the source of truth for each
operation's request and success-response contract:

- `/api/openapi/v1.json`
- `/api/openapi/v2.json`
- `/api/scalar`

## What changes in v2

Legacy v1 actions commonly catch exceptions inside controllers and return
controller-specific error bodies. Depending on the operation, the body may be a
plain string or another legacy JSON shape. Do not assume that all v1 errors have
one schema.

V2 actions allow classified exceptions to reach the global exception handlers.
These handlers return a consistent Problem Details JSON envelope with the
`application/problem+json` media type. A typical response contains:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found."
}
```

Problem Details members have these meanings:

| Member | Consumer guidance |
| --- | --- |
| `type` | Identifies the problem category. Treat it as an opaque URI unless the client explicitly supports that category. |
| `title` | A short, stable summary of the problem category. |
| `status` | The HTTP status represented by the body. Use the actual HTTP response status as the primary status value. |
| `detail` | A human-readable explanation. Do not use its wording as a programmatic identifier. |
| `instance` | Optional occurrence identifier. Clients must not require it. |
| `errors` | Validation errors keyed by request field. It appears only when field-level validation details are available. |

Additional extension members may be present. Consumers should ignore members
they do not recognize.

## Global exception status mapping

The current v2 global handlers map failures as follows:

| Failure | Status | Production `detail` behavior |
| --- | ---: | --- |
| Request or domain validation failure | `400 Bad Request` | Returns a client-safe validation message. |
| Required resource not found | `404 Not Found` | Uses `The requested resource was not found.` |
| Delete blocked by dependent resources | `409 Conflict` | Uses `The request conflicts with the current state of the resource.` |
| Unexpected or otherwise unmapped failure | `500 Internal Server Error` | Uses `An unexpected error occurred.` |

Development deployments may include the original exception message in 404,
409, and 500 responses. Production responses intentionally replace those
messages to avoid exposing implementation details. Consumers must not depend on
development-only wording.

Authentication, authorization, sensitivity checks, and other failures produced
before a controller action are handled by middleware and are outside the global
exception-handler contract described here. Test those responses separately for
the operations the application uses.

## Model-validation changes

Both versions return `400 Bad Request` with `application/problem+json` when
automatic model binding or data-annotation validation fails. The envelope and
validation field names differ.

Representative v1 validation response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "$.name": [
      "The name field is required."
    ]
  },
  "traceId": "..."
}
```

Representative v2 validation response:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "name": [
      "The name field is required."
    ]
  }
}
```

V2 removes JSON-path prefixes such as `$.` from validation keys. An error for
`$.name` in v1 is keyed by `name` in v2. A document-level error that cannot be
associated with a field uses `request`. V2 also removes serializer diagnostic
tails containing JSON paths, line numbers, byte positions, or internal type
details from validation messages.

Clients should render all messages in each `errors` entry and must not require a
`traceId`, `instance`, or `errors` member on every Problem Details response.

## Consumer parsing strategy

For every non-success response:

1. Branch on the HTTP status, not on error-message text.
2. If the response media type is `application/problem+json`, deserialize the
   Problem Details members that the application needs.
3. For validation responses, read all entries in `errors` and treat keys as
   field names. Update any v1 logic that expects a `$.` prefix.
4. Use `detail` for a human-readable message when present, then fall back to
   `title`, and finally to a status-based generic message.
5. During a staged migration, retain a fallback for legacy v1 plain-text or
   controller-specific JSON errors until the application sends no v1 traffic.
6. Ignore unknown extension members so the client remains forward compatible.

Do not make application decisions by comparing `title` or `detail` text. If a
workflow needs a stable machine-readable error identifier that the API does not
currently provide, coordinate that contract separately before migrating the
workflow.

## Migration verification

Before switching production traffic to v2:

- Test at least one successful response for every operation the application
  uses.
- Test malformed JSON and invalid field values that trigger automatic model
  validation.
- Test each expected 400, 404, and 409 workflow.
- Test an unexpected failure path and verify that the client handles a
  sanitized 500 response without requiring internal exception text.
- Test authentication and authorization failures separately.
- Verify that logs and user-facing messages do not expose raw response bodies or
  sensitive details.
- Run the application's complete regression suite against a non-production v2
  deployment.
- Change the API path to `/api/v2`, deploy with monitoring and a rollback plan,
  and confirm that the application no longer sends `/api/v1` traffic.

Repository-owned consumers must also follow the internal process in
[`api-consumer-versioning.md`](api-consumer-versioning.md).
