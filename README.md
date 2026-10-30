# DeepLynx images

This orphan branch only holds the workflow that builds the DeepLynx container
images and pushes them to ghcr.io. It shares no history with `main`, which
mirrors the internal repository.

## Building

```
gh workflow run build-images.yml --ref build-images -f ref=0.8.0 -f tag=0.8.0
```

- `ref`: the DeepLynx commit, tag or branch to build
- `tag`: the image tag, such as the release version
- `repository`: where `ref` comes from, `idaholab/DeepLynx` by default

The run stops before building anything if any of the images already has
`tag`, so a published tag is never overwritten. To rebuild a tag, delete it
from every image that has it first.

The workflow isn't on the default branch, so it doesn't show in the Actions
tab's "Run workflow" list; run it with `gh` as above.

## Images

Each is built for linux/amd64 and linux/arm64 from `Dockerfiles/<name>/Dockerfile.public`:

| Image | Port | |
|---|---|---|
| `ghcr.io/<owner>/deeplynx/deeplynx-server` | 5000 | API; applies its database migrations at startup |
| `ghcr.io/<owner>/deeplynx/deeplynx-ui` | 3000 | Web UI, built with frontend authentication disabled |
| `ghcr.io/<owner>/deeplynx/deeplynx-mcp` | 43656 | MCP server |
| `ghcr.io/<owner>/deeplynx/deeplynx-docs` | 3001 | Documentation site |

The database is the unmodified `pgvector/pgvector:0.8.3-pg18` image, so it
isn't built here. The insight services aren't built either: they live in an
internal submodule.

The UI is a Next.js app, which bakes `NEXT_PUBLIC_*` values into its bundle at
build time. The defaults (`api-url`, `docs-url`) match DeepLynx's
`docker-compose.yaml`: the browser reaches the API at
`http://localhost:5000/api` and the docs at `http://localhost:3001/docs`. The
UI's server side reads `BACKEND_BASE_URL`, `NEXTAUTH_SECRET` and the rest at
runtime, as in `docker-compose.yaml`.
