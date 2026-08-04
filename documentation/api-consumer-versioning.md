# Internal Nexus API consumer versioning

The UI and MCP server compose the Nexus API version in source code. Their base
URL environment variables identify only the server origin and optional
deployment base path:

- `BACKEND_BASE_URL` and `NEXT_PUBLIC_API_URL` for `deeplynx.UI`
- `NEXUS_API_URL` for `deeplynx.mcp`

For migration safety, both implementations accept a legacy base URL ending in
`/api/v1`. They reject a base URL ending in another API version, so an
environment-only edit cannot silently change the response contract.

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

## Internal consumer migration strategy

This strategy applies to repository-owned applications that call the Nexus API.
It describes changes to the consuming application, not how to implement or
version Nexus controller endpoints.

Migrate each consumer independently:

1. Inventory every v1 operation the consumer calls, including URLs assembled
   outside its shared HTTP client.
2. Compare each used operation in the v1 and v2 OpenAPI documents. Record any
   request, success-response, status-code, or authentication differences that
   require a consumer change.
3. Update the consumer's shared error handling according to the
   [v1-to-v2 error-response migration guide](api-v1-to-v2-error-response-migration.md).
   During a staged rollout, the client may need to accept both legacy v1 errors
   and v2 Problem Details responses.
4. Add contract tests for every operation the consumer uses. Cover successful
   responses and expected validation, authentication, authorization,
   not-found, conflict, and unexpected-error responses.
5. Change the consumer's version target in source code from v1 to v2. Do not
   change only an environment base URL; the environment variables identify the
   server origin, not the selected response contract.
6. Run the consumer's complete regression suite against a non-production v2
   deployment, including workflows that combine multiple API calls.
7. Deploy with monitoring and a documented rollback plan. Confirm through
   request telemetry that the migrated consumer no longer sends v1 traffic.

For `deeplynx.UI`, the reviewed cutover changes the target in
`src/app/lib/api-version.ts` and verifies both the shared fetch and Axios paths.
For `deeplynx.mcp`, the reviewed cutover changes `NexusApiVersions.Target` and
verifies every tool's non-success response handling. Track ownership, test
evidence, deployment status, and the migration-ticket link for each consumer in
the consumer decisions table or its linked ticket.
