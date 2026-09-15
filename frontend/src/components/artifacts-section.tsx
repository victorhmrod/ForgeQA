"use client";

import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ARTIFACT_TYPE_LABELS,
  ARTIFACT_TYPES,
  artifactsApi,
  formatBytes,
  uploadArtifact,
  type ArtifactType,
} from "@/lib/api/artifacts";
import { ApiError } from "@/lib/api/client";

export function ArtifactsSection({ projectId, buildId, buildArchived }: { projectId: string; buildId: string; buildArchived: boolean }) {
  const queryClient = useQueryClient();
  const [showUpload, setShowUpload] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [displayName, setDisplayName] = useState("");
  const [artifactType, setArtifactType] = useState<ArtifactType>("GAME_CLIENT");
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const queryKey = ["artifacts", projectId, buildId];
  const artifactsQuery = useQuery({
    queryKey,
    queryFn: () => artifactsApi.list(projectId, buildId),
  });

  const uploadMutation = useMutation({
    mutationFn: async () => {
      if (!file) throw new Error("Select a file to upload.");
      const controller = new AbortController();
      abortRef.current = controller;
      setUploadProgress(0);
      setUploadError(null);
      return uploadArtifact({
        projectId,
        buildId,
        file,
        displayName: displayName.trim() || file.name,
        artifactType,
        signal: controller.signal,
        onProgress: (uploaded, total) => setUploadProgress(total > 0 ? Math.min(100, Math.round((uploaded / total) * 100)) : 0),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      setShowUpload(false);
      setFile(null);
      setDisplayName("");
      setUploadProgress(0);
      abortRef.current = null;
    },
    onError: (error) => {
      setUploadError(error instanceof ApiError ? error.detail ?? error.message : error instanceof Error ? error.message : "Upload failed.");
      abortRef.current = null;
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (artifactId: string) => artifactsApi.delete(projectId, buildId, artifactId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
  });

  const downloadMutation = useMutation({
    mutationFn: (artifactId: string) => artifactsApi.createDownload(projectId, buildId, artifactId),
    onSuccess: ({ url }) => window.location.assign(url),
  });

  return (
    <section className="rounded-lg border border-border bg-surface p-4">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-muted">Artifacts</h2>
          <p className="mt-1 text-xs text-muted">Binary packages and symbols associated with this exact build.</p>
        </div>
        {!buildArchived && (
          <button
            type="button"
            onClick={() => setShowUpload((value) => !value)}
            className="rounded-md border border-border px-3 py-2 text-sm hover:bg-surface-hover"
          >
            {showUpload ? "Cancel" : "Upload artifact"}
          </button>
        )}
      </div>

      {showUpload && (
        <form
          className="mt-4 grid gap-3 rounded-md border border-border p-3"
          onSubmit={(event) => {
            event.preventDefault();
            if (!uploadMutation.isPending) uploadMutation.mutate();
          }}
        >
          <label className="grid gap-1 text-xs text-muted">
            File
            <input
              type="file"
              onChange={(event) => {
                const selected = event.target.files?.[0] ?? null;
                setFile(selected);
                if (selected && !displayName) setDisplayName(selected.name);
              }}
              className="rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
            />
          </label>
          <label className="grid gap-1 text-xs text-muted">
            Display name
            <input
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              placeholder="Windows Client"
              className="rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
            />
          </label>
          <label className="grid gap-1 text-xs text-muted">
            Type
            <select
              value={artifactType}
              onChange={(event) => setArtifactType(event.target.value as ArtifactType)}
              className="rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
            >
              {ARTIFACT_TYPES.map((type) => <option key={type} value={type}>{ARTIFACT_TYPE_LABELS[type]}</option>)}
            </select>
          </label>

          {file && <p className="text-xs text-muted">{file.name} · {formatBytes(file.size)}</p>}
          {uploadMutation.isPending && (
            <div>
              <div className="h-2 overflow-hidden rounded bg-background">
                <div className="h-full bg-accent transition-all" style={{ width: `${uploadProgress}%` }} />
              </div>
              <p className="mt-1 text-xs text-muted">Uploading {uploadProgress}%</p>
            </div>
          )}
          {uploadError && <p className="text-xs text-danger">{uploadError}</p>}

          <div className="flex gap-2">
            <button
              type="submit"
              disabled={!file || uploadMutation.isPending}
              className="rounded-md bg-accent px-3 py-2 text-sm text-background disabled:opacity-50"
            >
              Upload
            </button>
            {uploadMutation.isPending && (
              <button
                type="button"
                onClick={() => abortRef.current?.abort()}
                className="rounded-md border border-border px-3 py-2 text-sm text-danger"
              >
                Stop upload
              </button>
            )}
          </div>
        </form>
      )}

      <div className="mt-4">
        {artifactsQuery.isLoading && <p className="text-sm text-muted">Loading artifacts...</p>}
        {artifactsQuery.isError && <p className="text-sm text-danger">Artifacts could not be loaded.</p>}
        {artifactsQuery.data?.length === 0 && <p className="text-sm text-muted">No artifacts uploaded for this build.</p>}
        {!!artifactsQuery.data?.length && (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="text-xs text-muted">
                <tr className="border-b border-border">
                  <th className="py-2 pr-3 font-medium">Name</th>
                  <th className="py-2 pr-3 font-medium">Type</th>
                  <th className="py-2 pr-3 font-medium">Size</th>
                  <th className="py-2 pr-3 font-medium">Status</th>
                  <th className="py-2 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {artifactsQuery.data.map((artifact) => (
                  <tr key={artifact.id} className="border-b border-border last:border-0">
                    <td className="py-3 pr-3">
                      <div>{artifact.displayName}</div>
                      <div className="text-xs text-muted">{artifact.fileName}</div>
                    </td>
                    <td className="py-3 pr-3">{ARTIFACT_TYPE_LABELS[artifact.artifactType]}</td>
                    <td className="py-3 pr-3">{formatBytes(artifact.sizeBytes)}</td>
                    <td className="py-3 pr-3">{artifact.status}</td>
                    <td className="py-3">
                      <div className="flex gap-2">
                        {artifact.status === "READY" && (
                          <button type="button" onClick={() => downloadMutation.mutate(artifact.id)} className="text-accent hover:underline">
                            Download
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => {
                            if (window.confirm(`Delete ${artifact.displayName}?`)) deleteMutation.mutate(artifact.id);
                          }}
                          className="text-danger hover:underline"
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
