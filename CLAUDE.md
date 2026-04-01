# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DeepLynx Nexus is an enterprise knowledge graph and document management platform with integrated AI search. It is a multi-service monorepo containing a .NET 10 API backend, a Next.js frontend, a documentation site, and an optional Python AI/RAG service (Insight) as a Git submodule.

## Commands

### Backend (.NET)

```bash
# Build
dotnet build

# Run tests (requires Docker for Testcontainers)
dotnet test

# Run the API locally
cd deeplynx.api && dotnet run

# EF Migrations
dotnet ef migrations add <MigrationName> -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
dotnet ef database update -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
dotnet ef migrations list --project deeplynx.datalayer --startup-project deeplynx.api
dotnet ef migrations remove -c DeeplynxContext --verbose --project deeplynx.datalayer --startup-project deeplynx.api
```

### Frontend (Next.js)

```bash
cd deeplynx.UI

npm run dev        # Dev server on port 3000
npm run build      # Production build
npm run lint       # ESLint

# From repo root — run UI + docs concurrently
npm run all
```

### Docker

```bash
docker compose up --build                     # Core services only
docker compose --profile insight up --build   # Include Insight (AI search)
```

### Insight (Python — Git submodule at /Insight)

```bash
cd Insight
py -3.11 -m venv venv
uv sync --dev --active

# FastAPI + services tests
pytest app/tests/interfaces/ app/tests/api/ app/tests/services/ app/tests/utils/ -v

# RabbitMQ worker tests
PYTHONPATH=app/rabbitmq pytest app/tests/rabbitmq/ -v
```

## Architecture

### Backend Layers

```
deeplynx.api          → ASP.NET Core controllers (41 controllers), middleware, auth wiring
deeplynx.business     → Domain service classes (*Business.cs) — all logic lives here
deeplynx.datalayer    → EF Core models, DeeplynxContext, migrations, PostgreSQL
deeplynx.interfaces   → Interface definitions for DI
deeplynx.models       → Request/response DTOs
deeplynx.helpers      → Caching, middleware, shared utilities, exceptions
deeplynx.tests        → Integration tests using Testcontainers
deeplynx.mcp          → Model Context Protocol service
```

Controllers are thin — they delegate immediately to a corresponding `*Business` class. Each domain uses two DTO types: `*RequestDto` (POST/PUT input) and `*ResponseDto` (exposed output), both declared in the controller return type to appear in the Scalar API doc.

### Frontend

Next.js 16 + React 19, TypeScript, Material-UI 7, TailwindCSS + DaisyUI. Auth via NextAuth 5. Graph visualization via React Sigma/Graphology. Key areas:

- `deeplynx.UI/src/app/contexts/` — React context providers (org/project selection, RBAC)
- `deeplynx.UI/src/app/(home)/rbac/` — Permission-based component rendering
- `deeplynx.UI/auth.ts` — NextAuth configuration

### Insight (AI Search)

Optional submodule (`/Insight`). FastAPI REST + RabbitMQ async pipeline. Documents are uploaded, OCR'd, chunked, embedded, and stored in PostgreSQL with `pgvector`. Queries use vector similarity + streaming LLM responses with citations. Supports OpenAI-compatible endpoints (HPC, Ollama, vLLM).

Insight shares the same PostgreSQL instance as the core Nexus app but uses different env var names for the connection (`PG_HOST`, `PG_USER`, `PG_PASSWORD`, `PG_PORT`, `PG_DBNAME` vs. the Nexus `POSTGRES_*` variables).

### Storage Backends

File storage is swappable via env vars: local filesystem, Azure Blob Storage, or AWS S3/MinIO.

### Authentication

Supports both JWT (for local dev — set `DISABLE_BACKEND_AUTHENTICATION=true`) and OKTA (production, via OpenIdConnect).

## Development Notes

### Environment Setup

Copy `.env_sample` to `.env` before running locally. Check `.env_sample` for changes after pulling — they don't auto-apply to your `.env`.

### Git Submodule

Insight is a Git submodule. Clone with `--recurse-submodules` or run `git submodule update --init --recursive` after cloning.

### Testing Conventions

- Tests live in `deeplynx.tests/` and use Testcontainers (requires Docker).
- Use `[Collection("Test Suite Collection")]` and inherit from `TestSuiteFixture` to share one container per suite.
- After calling an EF SQL procedure in a test, call `Context.ChangeTracker.Clear()` to force a sync with DB state.

### Database Change Workflow

1. Edit model in `deeplynx.datalayer` (add columns, indexes, foreign keys — see CONTRIBUTING.md for patterns).
2. Update `DeeplynxContext.cs` for new relationships.
3. Create migration, verify with `database update`, then commit migration files.

### API Documentation

The app serves a Scalar API doc at `localhost:5095`. Use it for manual endpoint testing during development instead of Postman when convenient.

### Branch / PR Naming

Branch names should correspond to Jira ticket numbers (e.g., `DL-100`). PRs should be scoped to a single ticket when possible.
