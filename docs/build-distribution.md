# Build Distribution

Milestone 2 extends the Build Registry with artifact storage and direct distribution. A `Build` remains the logical identity of a compiled version; binaries are modeled separately as `BuildArtifact` records and temporary multipart state is modeled as `ArtifactUploadSession`.

## Model

```text
Organization
└── Project
    └── Build
        └── BuildArtifact
            └── ArtifactUploadSession
```

A build can have multiple artifacts, for example a Windows client, Linux dedicated server, or symbols archive. Archiving a build does not delete its artifacts. New uploads are blocked for archived builds, while existing ready artifacts remain downloadable.

## Storage architecture

ForgeQA uses an `IArtifactStorage` application abstraction with an S3-compatible infrastructure implementation. Development uses the MinIO service already present in Docker Compose. The bucket is private; application users never receive permanent storage credentials.

Large artifacts are uploaded directly from the browser to object storage using multipart presigned URLs:

```text
Browser -> ForgeQA API: initiate upload
ForgeQA API -> S3/MinIO: create multipart upload
ForgeQA API -> Browser: ForgeQA upload session
Browser -> ForgeQA API: request signed part URLs
Browser -> S3/MinIO: PUT file slices directly
Browser -> ForgeQA API: complete with part ETags
ForgeQA API -> S3/MinIO: complete + HEAD
ForgeQA API -> PostgreSQL: mark artifact READY
```

The backend does not proxy artifact bytes. Default part size is 64 MiB, browser upload concurrency is four parts, and signed part URLs are requested in small batches.

## Integrity

ForgeQA verifies that the completed object exists and that its storage-reported size exactly matches the registered `sizeBytes` before marking the artifact `READY`.

`sha256` is supported as normalized metadata. It is only compared when the active storage provider exposes a trustworthy full-object SHA-256 value. Multipart S3 ETags are never treated as SHA-256 or MD5 checksums.

## API

All artifact routes are scoped through both project and build ownership:

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/artifacts/uploads` | Initiate multipart upload |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/artifacts/{artifactId}/upload-parts` | Request signed part URLs |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/artifacts/{artifactId}/complete` | Finalize and verify upload |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/artifacts/{artifactId}/abort` | Abort multipart upload |
| `GET` | `/api/projects/{projectId}/builds/{buildId}/artifacts` | List build artifacts |
| `GET` | `/api/projects/{projectId}/builds/{buildId}/artifacts/{artifactId}` | Get artifact metadata |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/artifacts/{artifactId}/download` | Create short-lived download URL |
| `DELETE` | `/api/projects/{projectId}/builds/{buildId}/artifacts/{artifactId}` | Delete/tombstone artifact |

The server creates all object keys. Clients cannot choose a bucket path or use an artifact UUID to bypass project membership authorization.

## Local development

`docker compose up --build` starts PostgreSQL, MinIO, an idempotent `minio-init` bucket/CORS setup, the API, and the frontend.

Relevant environment variables:

- `MINIO_ENDPOINT` — backend-to-storage endpoint, normally `http://minio:9000` in Docker.
- `MINIO_PUBLIC_ENDPOINT` — hostname embedded in browser-facing presigned URLs, normally `http://localhost:9000`.
- `MINIO_BUCKET` — private artifact bucket.
- `ARTIFACT_PART_SIZE_BYTES` — multipart part size.
- `ARTIFACT_MAX_SIZE_BYTES` — maximum registered artifact size.
- `ARTIFACT_UPLOAD_SESSION_MINUTES` — upload-session lifetime.
- `ARTIFACT_DOWNLOAD_URL_MINUTES` — signed download lifetime.

MinIO CORS exposes the `ETag` response header because multipart completion requires the browser to return each uploaded part ETag to ForgeQA.

## Failure semantics

PostgreSQL and object storage do not share an ACID transaction. ForgeQA therefore models upload state explicitly (`PENDING`, `UPLOADING`, `VERIFYING`, `READY`, `FAILED`) and only makes downloads available from `READY` artifacts.

If completion or verification fails, the artifact is retained as failed metadata rather than falsely reported as ready. Upload sessions expire and can be aborted; a later cleanup job can remove stale provider multipart uploads without changing the artifact identity model.

## Deferred beyond M2

M2 does not implement a desktop launcher, installation, delta patching, release channels, tester groups, CI machine authentication, Unreal automatic upload, telemetry, bug reporting, or crash processing.
