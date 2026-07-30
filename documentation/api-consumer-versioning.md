# Internal Nexus API consumer versioning

The UI and MCP server compose the Nexus API version in source code. Their base
URL environment variables identify only the server origin and optional
deployment base path:

- `BACKEND_BASE_URL` and `NEXT_PUBLIC_API_URL` for `deeplynx.UI`
- `NEXUS_API_URL` for `deeplynx.mcp`

For migration safety, both implementations accept a legacy base URL ending in
`/api/v1`. They reject a base URL ending in another API version, so an
environment-only edit cannot silently change the response contract.

These are intentional compatibility pins even though v1 is frozen and
deprecated. v1 remains accessible with no announced removal date. Moving either
consumer to the forward-development v2 contract requires the reviewed cutover
described below.

## Consumer decisions

| Consumer | Current target | Decision and rationale |
| --- | --- | --- |
| Next.js UI proxy and client services | v1 | Pinned in `src/app/lib/api-version.ts`. This is a routing pin, not a contract cutover, so current behavior remains intact. The shared fetch and Axios paths can read legacy error packets and RFC 7807 `detail`, `title`, and validation `errors`, but adopting v2 remains a separately reviewed change. |
| `deeplynx.mcp` tools | v1 | Pinned in `AuthenticatedHttpClientFactory`. MCP tools keep their current v1 response/error behavior. Moving to v2 requires separately adopting and testing RFC 7807 ProblemDetails handling. |

The API server's configured default version is irrelevant to these consumers:
every UI backend URL is composed with `/api/v1`, and every relative MCP tool
path resolves beneath an `HttpClient.BaseAddress` ending in `/api/v1/`.

## Cutover rule

Changing either consumer to v2 requires a code review that changes its explicit
version constant and tests its handling of all non-2xx
`application/problem+json` responses. Do not perform a v2 cutover by editing a
base-URL environment variable.
