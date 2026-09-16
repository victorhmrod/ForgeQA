"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  apiKeysApi,
  PROJECT_API_KEY_SCOPES,
  PROJECT_API_KEY_SCOPE_LABELS,
  type ProjectApiKeyCreated,
  type ProjectApiKeyScope,
} from "@/lib/api/api-keys";
import { ApiError } from "@/lib/api/client";
import { Badge } from "@/components/badge";
import { FormField } from "@/components/form-field";

export default function ApiKeysPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const queryClient = useQueryClient();
  const queryKey = ["api-keys", projectId];

  const { data: keys, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => apiKeysApi.list(projectId),
  });

  const [name, setName] = useState("");
  const [scopes, setScopes] = useState<ProjectApiKeyScope[]>(["BUG_REPORT_WRITE"]);
  const [error, setError] = useState<string | null>(null);
  const [justCreated, setJustCreated] = useState<ProjectApiKeyCreated | null>(null);

  function toggleScope(scope: ProjectApiKeyScope) {
    setScopes((current) => (current.includes(scope) ? current.filter((s) => s !== scope) : [...current, scope]));
  }

  const createMutation = useMutation({
    mutationFn: () => apiKeysApi.create(projectId, name, scopes),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey });
      setJustCreated(created);
      setName("");
      setError(null);
    },
    onError: (err) => setError(err instanceof ApiError ? err.detail ?? err.message : "Something went wrong."),
  });

  const revokeMutation = useMutation({
    mutationFn: (keyId: string) => apiKeysApi.revoke(projectId, keyId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
  });

  return (
    <main className="mx-auto max-w-3xl px-6 py-8">
      <h2 className="mb-1 text-lg font-semibold">API Keys</h2>
      <p className="mb-6 text-sm text-muted">
        Project-scoped runtime credentials. Paste one into the ForgeQA Unreal plugin so a packaged
        game can submit bug reports without a developer account. A key can only submit bug reports
        and attachments &mdash; it cannot read data or manage the project.
      </p>

      {justCreated && (
        <div className="mb-6 rounded-lg border border-accent/40 bg-accent/5 p-4">
          <p className="text-sm font-medium">Copy this key now. It cannot be shown again.</p>
          <code className="mt-2 block break-all rounded-md bg-background px-3 py-2 text-xs">{justCreated.plaintextKey}</code>
          <button
            onClick={() => setJustCreated(null)}
            className="mt-3 rounded-md border border-border px-3 py-1.5 text-xs hover:bg-surface-hover"
          >
            Done
          </button>
        </div>
      )}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!name.trim()) {
            setError("Name is required.");
            return;
          }
          if (scopes.length === 0) {
            setError("Select at least one scope.");
            return;
          }
          createMutation.mutate();
        }}
        className="mb-6 flex flex-col gap-3 rounded-lg border border-border bg-surface p-4"
      >
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <FormField label="Name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Unreal Runtime" />
          </div>
          <button
            type="submit"
            disabled={createMutation.isPending}
            className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground disabled:opacity-60"
          >
            {createMutation.isPending ? "Creating..." : "Create key"}
          </button>
        </div>

        <fieldset className="flex flex-col gap-1.5">
          <legend className="mb-1 text-sm font-medium text-muted">Scopes</legend>
          {PROJECT_API_KEY_SCOPES.map((scope) => (
            <label key={scope} className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={scopes.includes(scope)} onChange={() => toggleScope(scope)} />
              {PROJECT_API_KEY_SCOPE_LABELS[scope]}
            </label>
          ))}
        </fieldset>
      </form>
      {error && <p className="mb-4 text-sm text-danger">{error}</p>}

      {isLoading && <p className="text-sm text-muted">Loading API keys...</p>}
      {isError && <p className="text-sm text-danger">Failed to load API keys.</p>}

      {!isLoading && !isError && keys?.length === 0 && (
        <p className="text-sm text-muted">No API keys yet. Create one to link the Unreal plugin.</p>
      )}

      {!isLoading && !isError && keys && keys.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-border">
          <table className="w-full text-left text-sm">
            <thead className="bg-surface text-xs uppercase tracking-wide text-muted">
              <tr>
                <th className="px-4 py-3 font-medium">Name</th>
                <th className="px-4 py-3 font-medium">Prefix</th>
                <th className="px-4 py-3 font-medium">Scopes</th>
                <th className="px-4 py-3 font-medium">Created</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium" />
              </tr>
            </thead>
            <tbody>
              {keys.map((key) => (
                <tr key={key.id} className="border-t border-border">
                  <td className="px-4 py-3">{key.name}</td>
                  <td className="px-4 py-3 font-mono text-xs text-muted">fqa_proj_{key.prefix}...</td>
                  <td className="px-4 py-3 text-muted">{key.scopes.map((s) => PROJECT_API_KEY_SCOPE_LABELS[s]).join(", ")}</td>
                  <td className="px-4 py-3 text-muted">{new Date(key.createdAt).toLocaleDateString()}</td>
                  <td className="px-4 py-3">
                    <Badge tone={key.revokedAt ? "muted" : "success"}>{key.revokedAt ? "Revoked" : "Active"}</Badge>
                  </td>
                  <td className="px-4 py-3 text-right">
                    {!key.revokedAt && (
                      <button
                        onClick={() => revokeMutation.mutate(key.id)}
                        className="text-xs text-danger hover:underline"
                      >
                        Revoke
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  );
}
