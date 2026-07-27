# File content hashing

Nexus records a SHA-256 hash for the complete byte content of supported stored files. The hash supports content identity, integrity verification, traceability, and provenance without depending on a file name, record ID, or storage URI.

## Hash definition

`file_content_hash` is the lowercase hexadecimal SHA-256 digest of the file bytes supplied to the storage provider. Nexus does not normalize text, parse documents, hash generated chunks, or hash embedding vectors as part of this feature.

Two files with identical bytes have the same content hash. Any byte-level difference produces a different hash. The hash is therefore the canonical identity of file content, while comparisons against a recomputed digest provide artifact verification.

SHA-256 digests are stored as 64 lowercase hexadecimal characters. The digest does not include the storage location, file name, metadata, content length, or other record fields.

## Upload and update flow

The existing file workflow asks the selected storage provider to calculate a content hash before it stores the file.

1. `FileBusiness` resolves the configured object-storage provider.
2. The provider calculates a content hash when hashing is supported.
3. The provider uploads or replaces the file through its existing implementation.
4. `FileBusiness` includes the hash when it creates or updates the associated record.
5. Nexus returns the hash as `fileContentHash` in `RecordResponseDto`.

Azure hashing is implemented in `FileAzureBusiness`. It streams the upload through the shared `Sha256HashHelper`, so the entire file does not need to be held in memory. Hash calculation is synchronous and adds one read of the incoming upload stream before the Azure upload begins. It does not reread the completed blob from Azure.

For completed chunked Azure uploads, where the original `IFormFile` is no longer available, `FileAzureBusiness` streams the finalized blob once to calculate its digest before the record is created.

`FileFilesystemBusiness` and `FileS3Business` currently return a null hash from the same provider hook. These placeholders keep the provider contract stable without changing existing filesystem or S3 upload behavior. Their hashing implementations are deferred to provider-specific follow-up tickets.

When a file is replaced through a provider that does not yet calculate hashes, Nexus clears the previous hash rather than retaining a digest for bytes that no longer exist.

## Updating or backfilling a hash

An authenticated provider-neutral endpoint can store a hash for a specific file record:

```text
PUT /organizations/{organizationId}/projects/{projectId}/files/{recordId}/hash
```

The caller must have `update file` and `update record` permissions. The request contains:

- `hashAlgorithm`: must be `SHA-256`;
- `hashHex`: a 64-character hexadecimal digest;
- `contentLength`: optional nonnegative file length used for verification.

The endpoint identifies the record directly from the route and does not depend on a storage-provider type, container, or object path. When both values are available, Nexus verifies that `contentLength` equals the record's stored file size. Hashes are normalized to lowercase.

Submitting the hash already stored on the record is idempotent. A changed hash updates the record and creates update-event and provenance entries.

The endpoint supports future provider integrations and controlled backfill tools, but this change does not include a job that enumerates existing files. Existing records with a null `file_content_hash` remain unhashed until their file is replaced through a supported provider or an external backfill submits a verified digest.

## Validation responses

- Invalid algorithm, digest, or content length: `400 Bad Request`.
- Missing or archived record: `404 Not Found`.
- Content-length mismatch: `409 Conflict`.
- Successful or idempotent update: `200 OK` with `RecordResponseDto`.

## Current provider scope

| Provider | Automatic upload hash | Automatic update hash |
| --- | --- | --- |
| Azure Blob Storage | SHA-256 | SHA-256 |
| Filesystem | Deferred; returns null | Deferred; clears a previous hash |
| Amazon S3 | Deferred; returns null | Deferred; clears a previous hash |

No database migration is required for this change because `records.file_content_hash` already exists in the current schema.
