import { apiClient } from "./client";

export type ArtifactType = "GAME_CLIENT" | "DEDICATED_SERVER" | "SYMBOLS" | "OTHER";
export type ArtifactStatus = "PENDING" | "UPLOADING" | "VERIFYING" | "READY" | "FAILED";

export const ARTIFACT_TYPES: ArtifactType[] = ["GAME_CLIENT", "DEDICATED_SERVER", "SYMBOLS", "OTHER"];
export const ARTIFACT_TYPE_LABELS: Record<ArtifactType, string> = {
  GAME_CLIENT: "Game Client",
  DEDICATED_SERVER: "Dedicated Server",
  SYMBOLS: "Symbols",
  OTHER: "Other",
};

export interface BuildArtifact {
  id: string;
  buildId: string;
  fileName: string;
  displayName: string;
  artifactType: ArtifactType;
  contentType: string;
  sizeBytes: number;
  sha256: string | null;
  status: ArtifactStatus;
  createdBy: { id: string; name: string };
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  deletedAt: string | null;
}

export interface InitiateArtifactUploadInput {
  fileName: string;
  displayName: string;
  artifactType: ArtifactType;
  contentType?: string;
  sizeBytes: number;
  sha256?: string;
}

export interface InitiateArtifactUploadResponse {
  artifactId: string;
  uploadSessionId: string;
  partSizeBytes: number;
  partCount: number;
  expiresAt: string;
}

interface UploadPartUrl {
  partNumber: number;
  url: string;
}

interface DownloadResponse {
  url: string;
  expiresAt: string;
}

const basePath = (projectId: string, buildId: string) =>
  `/api/projects/${projectId}/builds/${buildId}/artifacts`;

export const artifactsApi = {
  list: (projectId: string, buildId: string) =>
    apiClient.get<BuildArtifact[]>(basePath(projectId, buildId)),

  get: (projectId: string, buildId: string, artifactId: string) =>
    apiClient.get<BuildArtifact>(`${basePath(projectId, buildId)}/${artifactId}`),

  initiate: (projectId: string, buildId: string, input: InitiateArtifactUploadInput) =>
    apiClient.post<InitiateArtifactUploadResponse>(`${basePath(projectId, buildId)}/uploads`, input),

  getPartUrls: (projectId: string, buildId: string, artifactId: string, uploadSessionId: string, partNumbers: number[]) =>
    apiClient.post<{ parts: UploadPartUrl[] }>(`${basePath(projectId, buildId)}/${artifactId}/upload-parts`, {
      uploadSessionId,
      partNumbers,
    }),

  complete: (
    projectId: string,
    buildId: string,
    artifactId: string,
    uploadSessionId: string,
    parts: { partNumber: number; eTag: string }[],
  ) =>
    apiClient.post<BuildArtifact>(`${basePath(projectId, buildId)}/${artifactId}/complete`, {
      uploadSessionId,
      parts,
    }),

  abort: (projectId: string, buildId: string, artifactId: string, uploadSessionId: string) =>
    apiClient.post<BuildArtifact>(`${basePath(projectId, buildId)}/${artifactId}/abort`, { uploadSessionId }),

  createDownload: (projectId: string, buildId: string, artifactId: string) =>
    apiClient.post<DownloadResponse>(`${basePath(projectId, buildId)}/${artifactId}/download`),

  delete: (projectId: string, buildId: string, artifactId: string) =>
    apiClient.delete<BuildArtifact>(`${basePath(projectId, buildId)}/${artifactId}`),
};

export async function uploadArtifact(options: {
  projectId: string;
  buildId: string;
  file: File;
  displayName: string;
  artifactType: ArtifactType;
  signal: AbortSignal;
  onProgress: (uploadedBytes: number, totalBytes: number) => void;
}): Promise<BuildArtifact> {
  const { projectId, buildId, file, displayName, artifactType, signal, onProgress } = options;
  const upload = await artifactsApi.initiate(projectId, buildId, {
    fileName: file.name,
    displayName,
    artifactType,
    contentType: file.type || "application/octet-stream",
    sizeBytes: file.size,
  });

  const completedParts: { partNumber: number; eTag: string }[] = [];
  const progressByPart = new Map<number, number>();

  try {
    const allPartNumbers = Array.from({ length: upload.partCount }, (_, index) => index + 1);
    const urlBatchSize = 20;

    for (let offset = 0; offset < allPartNumbers.length; offset += urlBatchSize) {
      if (signal.aborted) throw new DOMException("Upload cancelled", "AbortError");

      const partNumbers = allPartNumbers.slice(offset, offset + urlBatchSize);
      const { parts } = await artifactsApi.getPartUrls(projectId, buildId, upload.artifactId, upload.uploadSessionId, partNumbers);

      let nextIndex = 0;
      const workers = Array.from({ length: Math.min(4, parts.length) }, async () => {
        while (nextIndex < parts.length) {
          const index = nextIndex++;
          const part = parts[index];
          const start = (part.partNumber - 1) * upload.partSizeBytes;
          const end = Math.min(start + upload.partSizeBytes, file.size);
          const blob = file.slice(start, end);

          const eTag = await uploadPart(part.url, blob, signal, (loaded) => {
            progressByPart.set(part.partNumber, loaded);
            const uploadedBytes = Array.from(progressByPart.values()).reduce((sum, value) => sum + value, 0);
            onProgress(uploadedBytes, file.size);
          });

          progressByPart.set(part.partNumber, blob.size);
          completedParts.push({ partNumber: part.partNumber, eTag });
          const uploadedBytes = Array.from(progressByPart.values()).reduce((sum, value) => sum + value, 0);
          onProgress(uploadedBytes, file.size);
        }
      });

      await Promise.all(workers);
    }

    completedParts.sort((a, b) => a.partNumber - b.partNumber);
    return await artifactsApi.complete(projectId, buildId, upload.artifactId, upload.uploadSessionId, completedParts);
  } catch (error) {
    try {
      await artifactsApi.abort(projectId, buildId, upload.artifactId, upload.uploadSessionId);
    } catch {
      // The server-side session expires and can be cleaned up even when explicit abort fails.
    }
    throw error;
  }
}

function uploadPart(url: string, blob: Blob, signal: AbortSignal, onProgress: (loaded: number) => void): Promise<string> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    const abort = () => xhr.abort();
    signal.addEventListener("abort", abort, { once: true });

    xhr.open("PUT", url);
    xhr.upload.onprogress = (event) => onProgress(event.loaded);
    xhr.onerror = () => reject(new Error("Artifact part upload failed."));
    xhr.onabort = () => reject(new DOMException("Upload cancelled", "AbortError"));
    xhr.onload = () => {
      signal.removeEventListener("abort", abort);
      if (xhr.status < 200 || xhr.status >= 300) {
        reject(new Error(`Artifact part upload failed with status ${xhr.status}.`));
        return;
      }

      const eTag = xhr.getResponseHeader("ETag");
      if (!eTag) {
        reject(new Error("Object storage did not expose the uploaded part ETag."));
        return;
      }

      resolve(eTag);
    };
    xhr.send(blob);
  });
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ["KB", "MB", "GB", "TB"];
  let value = bytes / 1024;
  let unit = units[0];
  for (let index = 1; index < units.length && value >= 1024; index++) {
    value /= 1024;
    unit = units[index];
  }
  return `${value >= 10 ? value.toFixed(1) : value.toFixed(2)} ${unit}`;
}
