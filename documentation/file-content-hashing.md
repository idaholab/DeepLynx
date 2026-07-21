# File content hashing

Nexus records a SHA-256 hash for the complete byte content of files handled by its hashing integrations. The hash supports content identity, integrity verification, traceability, and provenance without depending on a file name, record ID, or storage URI.

## Hash definition

`file_content_hash` is the lowercase hexadecimal SHA-256 digest of the file bytes exactly as stored in object storage. Nexus does not normalize text, parse documents, hash generated chunks, or hash embedding vectors as part of this feature.

Two files with identical bytes have the same content hash. Any byte-level difference produces a different hash. The hash is therefore the canonical identity of file content, while comparisons against a recomputed digest provide artifact verification.

SHA-256 digests are stored as 64 lowercase hexadecimal characters. The hash does not include the container name, blob name, metadata, content length, or other record fields.

## Azure hashing flow

The `deeplynx.blobhash.functions` Azure Function watches the container configured by `BLOB_HASH_CONTAINER`.

1. The blob trigger receives a completed blob as a stream.
2. Temporary paths containing `/uploads/` and paths outside the Nexus naming convention are ignored.
3. The function computes SHA-256 incrementally from the blob stream so the entire file does not need to be loaded into memory.
4. The function authenticates to Nexus with its configured API key and secret.
5. It posts the digest, container, blob name, and available content length to the internal blob-hash callback.
6. Nexus matches an active Azure-backed record by organization, project, blob name, and configured Azure container.
7. When both values are available, Nexus verifies that the callback content length equals the record's stored file size.
8. Nexus stores the digest in `records.file_content_hash` and records an update event. Repeating a callback with the same digest is idempotent.

The container and content length are verification inputs; they are not part of the digest. Container matching prevents a hash from being associated with a same-named blob in another container. Content-length comparison catches callbacks for bytes that do not match the record's expected file size.

## Callback validation

The callback accepts only:

- object storage type `azure_object`;
- hash algorithm `SHA-256`;
- a nonempty container and blob name;
- a 64-character hexadecimal digest;
- a nonnegative content length when one is supplied.

A missing record or container match returns `404 Not Found`. Ambiguous matches or a content-length mismatch return `409 Conflict`. Invalid callback values return `400 Bad Request`.

The Function retries transient callback failures, including temporary authorization failures, rate limiting, server errors, and a record that is not yet visible. Validation and conflict responses are treated as permanent failures.

## Configuration

The Function uses these environment variables:

- `AzureWebJobsStorage`: Azure Storage connection used by the blob trigger.
- `BLOB_HASH_CONTAINER`: watched Azure container and the container identity sent to Nexus.
- `NEXUS_BASE_URL`: Nexus API base URL, including its API version prefix.
- `NEXUS_API_KEY` and `NEXUS_API_SECRET`: service credentials used to request a Nexus token.
- `NEXUS_TOKEN_EXPIRATION_MINUTES`: requested token lifetime; defaults to 55 minutes.
- `NEXUS_CALLBACK_MAX_ATTEMPTS`: maximum callback attempts; defaults to 5.
- `NEXUS_CALLBACK_BASE_DELAY_SECONDS`: initial exponential retry delay; defaults to 2 seconds.

See `deeplynx.blobhash.functions/local.settings.sample.json` for a local configuration template. Do not commit real credentials.

## Current scope

The automated hashing worker currently covers Nexus files stored in the configured Azure Blob Storage container. Filesystem and S3 storage providers require equivalent hashing integration before they can provide the same automatic guarantee.
