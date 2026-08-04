# Internal Nexus API consumer versioning

The UI and MCP server compose the Nexus API version separately from their base
URLs:

- `BACKEND_BASE_URL` and `NEXT_PUBLIC_API_URL` identify the UI's `/api` base
  path, while `NEXT_PUBLIC_API_VERSION` selects its default version (`v1` when
  omitted)
- `NEXUS_API_URL` for `deeplynx.mcp`

The UI also accepts host-only and legacy versioned base URLs during migration.
All UI services use the same configured version. The backend is responsible for
exposing a complete API surface at that version, including unchanged behavior.
The MCP consumer remains pinned to v1.

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
| Next.js UI proxy and client services | Configurable; v1 fallback | `NEXT_PUBLIC_API_VERSION` selects one version for the entire UI deployment. The backend exposes changed and unchanged endpoints beneath that version. The shared fetch and Axios paths can read legacy error packets and RFC 7807 `detail`, `title`, and validation `errors`. |
| `deeplynx.mcp` tools | v1 | Pinned in `AuthenticatedHttpClientFactory`. MCP tools keep their current v1 response/error behavior. Moving to v2 requires separately adopting and testing RFC 7807 ProblemDetails handling. |

The API server's configured default version is irrelevant to these consumers:
the UI always composes an explicit `/api/{version}` segment, and every relative
MCP tool path resolves beneath an `HttpClient.BaseAddress` ending in `/api/v1/`.

Browser services share one Axios client in
`deeplynx.UI/src/app/lib/client_service/api.ts`. It follows the deployment-wide
configured version and owns the authentication, single-flight session lookup,
and error interceptors.

## Cutover rule

Moving the UI to another API version is a deployment-wide contract cutover. The
backend must make all required endpoints available at that version, and the UI
must test affected response contracts and all non-2xx
`application/problem+json` responses. Moving the MCP consumer still requires a
code change and its own contract review.
