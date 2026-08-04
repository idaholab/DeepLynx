# Internal Nexus API consumer versioning

The UI and MCP server compose the Nexus API version separately from their base
URLs:

- `BACKEND_BASE_URL` and `NEXT_PUBLIC_API_URL` identify the UI's `/api` base
  path, while `NEXT_PUBLIC_API_VERSION` selects its default version (`v1` when
  omitted)
- `NEXUS_API_URL` for `deeplynx.mcp`

The UI also accepts host-only and legacy versioned base URLs during migration.
An individual UI service can explicitly override the configured default when
its response contract has moved to another version. The MCP consumer remains
pinned to v1.

## UI configuration

Configure local UI development with:

```dotenv
NEXT_PUBLIC_API_URL=http://localhost:5095/api
NEXT_PUBLIC_API_VERSION=v1
BACKEND_BASE_URL=http://localhost:5095/api
```

`NEXT_PUBLIC_API_VERSION` must be `v` followed by a positive integer, such as
`v1`, `v2`, or `v12`. Values are normalized to lowercase. If the variable is
missing or empty, the UI safely falls back to `v1`; malformed non-empty values
fail validation instead of producing an invalid backend URL.

Because `NEXT_PUBLIC_API_VERSION` and `NEXT_PUBLIC_API_URL` are exposed to
browser code by Next.js, deployments must provide them when building the UI
image. `BACKEND_BASE_URL` remains server-only and may use an internal hostname.

## Consumer decisions

| Consumer | Current target | Decision and rationale |
| --- | --- | --- |
| Next.js UI proxy and client services | Configurable; v1 fallback | `NEXT_PUBLIC_API_VERSION` selects the default. Services may explicitly select another validated version when adopting its response contract. The shared fetch and Axios paths can read legacy error packets and RFC 7807 `detail`, `title`, and validation `errors`. |
| `deeplynx.mcp` tools | v1 | Pinned in `AuthenticatedHttpClientFactory`. MCP tools keep their current v1 response/error behavior. Moving to v2 requires separately adopting and testing RFC 7807 ProblemDetails handling. |

The API server's configured default version is irrelevant to these consumers:
the UI always composes an explicit `/api/{version}` segment, and every relative
MCP tool path resolves beneath an `HttpClient.BaseAddress` ending in `/api/v1/`.

## Cutover rule

Moving a UI service to v2 requires an explicit service override or a reviewed
default-version change, plus tests for its response contract and all non-2xx
`application/problem+json` responses. Moving the MCP consumer still requires a
code change and its own contract review.
