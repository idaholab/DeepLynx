# DL-1254 Provenance Chain PR Split Plan

## Purpose

Replace the signing-authority-based provenance integrity design with a hash-chained
provenance model that requires no trust/PKI chain of any kind — no external KMS, no
self-managed signing keys, no authority whose keys anyone has to trust. Split the
work into five independently mergeable PRs.

## Why the Signing-Authority Design Was Dropped

Current team guidance disallows establishing any signing authority for provenance,
including externally-hosted KMS (OpenBao Transit, AWS KMS) and self-managed signing
keys. This supersedes the prior branch stack (`DL-1254-openbao-kms`,
`DL-1254-signature-persistence`, `DL-1254-signing-lifecycle`,
`DL-1254-insight-signatures`). Those branches are left in place for reference/salvage
(canonical envelope construction, in particular, carries forward unchanged) but are
not the path forward. Decide whether to delete them once this plan's PR 1 has landed
and the canonical envelope logic has been re-homed.

## Design

- Each provenance record's canonical envelope hash includes the previous record's
  chain hash, making history tamper-evident: altering any past record changes its
  hash and breaks every chain hash after it.
- Reuses the existing canonical envelope construction (`ProvenanceCanonicalEnvelope`)
  from the superseded signature-persistence work — only the chaining and persistence
  around it change.
- DB-level immutability (revoke `UPDATE`/`DELETE` on the provenance table, or a
  trigger) prevents rewriting history at the storage layer, complementing the chain.
  Enforcement lands only after chain backfill so existing rows can still be linked in.
- Verification recomputes the chain from the beginning (or from a checkpoint) and
  compares hashes — no external call, no keys, no network dependency.
- Forward compatibility: keep a nullable signature surface (`SigningStatus =
  Disabled`, nullable `Signature`/`SigningProvider`/`SigningKeyReference`, an unused
  `IProvenanceSigningProvider`-style interface) so that if the team later approves a
  specific signing approach, it can sign the existing chain hash without schema
  rework or re-chaining history. No provider is implemented now.

## Branches and Dependency Order

1. `DL-1254-provenance-chain-persistence` → `develop`
2. `DL-1254-provenance-chain-immutability` → PR 1
3. `DL-1254-provenance-project-history` → PR 2
4. `DL-1254-provenance-chain-verification` → PR 1
5. `DL-1254-insight-completion-tracking` → `develop` (independent of 1–4)

PRs 2, 3, and 4 all depend on PR 1's schema but not on each other (PR 3 additionally
depends on PR 2's dropped-cascading-FK migration, since that's what lets a project's
provenance survive deletion of its records — see PR 3 below), so they can be
developed in parallel with PR 4. PR 5 does not touch chain code at all — it creates
ordinary provenance records on Insight completion, which PR 1's logic chains
automatically once merged — so it can be built and merged independently of PRs 1–4.

## PR 1: Chain Persistence

Branch: `DL-1254-provenance-chain-persistence`

Scope:

- Add `previous_hash`/`chain_hash` columns to the provenance record table (or
  equivalent chain-link model).
- Compute the chain hash from the canonical envelope hash and the prior record's
  chain hash at creation time.
- Add the nullable, unused signature/signing-key surface described above for future
  compatibility; no provider, no signing behavior.
- Backfill chain hashes for existing provenance records.
- Generate a new EF migration containing only these schema changes.

Validation:

- Test deterministic canonical envelope hashing (carried over) and chain-hash
  computation.
- Test that altering a historical record's stored envelope changes its own hash and
  every descendant chain hash.
- Apply the migration to a clean database, including backfill, and validate.

## PR 2: Chain Immutability

Branch: `DL-1254-provenance-chain-immutability`

Depends on: PR 1

Scope:

- Enforce append-only behavior on the provenance table at the database level (revoke
  `UPDATE`/`DELETE` privileges from the application role, or an equivalent trigger).
- Confirm the enforcement does not block legitimate application writes (inserts,
  chain-hash backfill migrations run before lockdown).

Validation:

- Test that an `UPDATE`/`DELETE` attempt against the provenance table fails.
- Test that ordinary provenance creation still succeeds.
- Validate against a clean database with the PR 1 migration and backfill applied
  first, then immutability enforcement added.

## PR 3: Project-Level Provenance History

Branch: `DL-1254-provenance-project-history`

Depends on: PR 2 (needs the dropped cascading FKs so a project's provenance survives
deletion of the records it describes)

Scope:

- Add a project-scoped provenance history endpoint (`GET .../project-history`) that
  returns every provenance record ever created for a project, including provenance
  for records/historical records that have since been deleted — no per-record
  existence check, unlike the existing `GetProvenanceHistory`.
- Paginated via the existing `PaginatedRequestDto`/`PaginatedResponse<T>` convention.
- The existing per-record `GetProvenanceHistory` endpoint's 404-on-deleted-record
  behavior is left unchanged; this new endpoint is the sanctioned way to see history
  after a record's deleted.
- Open item to decide at kickoff: whether to add an optional `recordId` filter on
  this endpoint so a client can jump straight to one record's history within a
  project instead of paging through the whole project's provenance.

Full implementation plan (auth-attribute decision, exact method signatures, route
strings, DTO reuse, ordering/pagination-safety details, and test list) is written up
separately; see the plan captured for this feature when PR 3 work starts.

Validation:

- Test that a deleted record's provenance still appears in the project-level history.
- Test pagination, ordering (including `CreatedAt`-tie handling from batch-created
  provenance rows), and empty-history cases.
- Test that provenance from other projects is excluded and a nonexistent project
  404s.

## PR 4: Chain Verification

Branch: `DL-1254-provenance-chain-verification`

Depends on: PR 1

Scope:

- Add a verification endpoint/service that recomputes the canonical envelope hash
  and chain hash for a record (and optionally walks back to the start of the chain
  or a checkpoint) and reports whether it matches the stored value.
- No background worker, no retries, no external provider — this is a synchronous,
  local recomputation.

Validation:

- Test verification success on an untampered chain.
- Test verification failure when a historical record's envelope or a chain hash has
  been altered.
- Test verification behavior at the start of a chain (no `previous_hash`) and across
  a project/org boundary if chains are scoped that way.

## PR 5: Insight Completion Tracking

Branch: `DL-1254-insight-completion-tracking`

Depends on: `develop` only

Scope:

- Add the Insight completion tracker model, migration, and historical-record
  pinning (carried over from the superseded `DL-1254-insight-signatures` branch).
- Add the hosted `InsightCompletionProcessor`/`InsightCompletionWorker` that polls
  Insight for terminal results using a lease-based batch pattern.
- On a terminal result, create an ordinary `insight-ingestion-complete` or
  `insight-ingestion-failed` provenance event pinned to the historical record
  captured at request time. No signing-specific queuing logic — the event is chained
  automatically by PR 1's logic once that PR has merged.
- Tracker supersession when a new Insight run is requested for a record with an
  in-flight tracker, and `TimedOut` handling after 24 hours without a terminal
  result.

Validation:

- Test completion tracking and idempotency.
- Test tracker supersession and timeout handling.
- Test that both terminal outcomes produce a provenance record, and that neither
  path depends on any signing/chain-specific code.

## Resume in a New Codex Context

Ask Codex to read this file and the repository `AGENTS.md`/`CLAUDE.md`, then continue
with a specific branch. For example:

> Read `documentation/DL-1254-signature-pr-split-plan.md` and `CLAUDE.md`. Continue
> implementing the hash-chained provenance design, starting with
> `DL-1254-provenance-chain-persistence`. Keep the PR boundaries described in the
> plan, and do not introduce any signing authority, KMS dependency, or PKI trust
> chain.
