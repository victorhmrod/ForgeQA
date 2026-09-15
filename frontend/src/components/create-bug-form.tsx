"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { buildsApi } from "@/lib/api/builds";
import { BUG_SEVERITIES, SEVERITY_LABELS, type BugSeverity, type CreateBugInput } from "@/lib/api/bugs";
import { FormField } from "@/components/form-field";

interface CreateBugFormProps {
  projectId: string;
  onSubmit: (input: CreateBugInput) => Promise<void>;
  onCancel: () => void;
}

export function CreateBugForm({ projectId, onSubmit, onCancel }: CreateBugFormProps) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [reproductionSteps, setReproductionSteps] = useState("");
  const [severity, setSeverity] = useState<BugSeverity>("MEDIUM");
  const [buildId, setBuildId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { data: builds } = useQuery({
    queryKey: ["builds", projectId, { page: 1, pageSize: 100 }],
    queryFn: () => buildsApi.list(projectId, { page: 1, pageSize: 100 }),
  });

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!title.trim()) {
      setError("Title is required.");
      return;
    }

    setIsSubmitting(true);
    try {
      await onSubmit({
        title,
        description: description || undefined,
        reproductionSteps: reproductionSteps || undefined,
        severity,
        source: "WEB",
        buildId: buildId || undefined,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4">
      <FormField label="Title" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Weapon remains ADS after reload" />

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted font-medium">Severity</span>
          <select
            value={severity}
            onChange={(e) => setSeverity(e.target.value as BugSeverity)}
            className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-accent focus:ring-1 focus:ring-accent"
          >
            {BUG_SEVERITIES.map((s) => (
              <option key={s} value={s}>
                {SEVERITY_LABELS[s]}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted font-medium">Build (optional)</span>
          <select
            value={buildId}
            onChange={(e) => setBuildId(e.target.value)}
            className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-accent focus:ring-1 focus:ring-accent"
          >
            <option value="">No specific build</option>
            {builds?.items.map((build) => (
              <option key={build.id} value={build.id}>
                {build.name ?? `${build.version} (${build.buildNumber})`}
              </option>
            ))}
          </select>
        </label>
      </div>

      <label className="flex flex-col gap-1.5 text-sm">
        <span className="text-muted font-medium">Description</span>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={3}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        />
      </label>

      <label className="flex flex-col gap-1.5 text-sm">
        <span className="text-muted font-medium">Steps to reproduce</span>
        <textarea
          value={reproductionSteps}
          onChange={(e) => setReproductionSteps(e.target.value)}
          rows={3}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-accent focus:ring-1 focus:ring-accent"
          placeholder={"1. ADS\n2. Reload\n3. Release ADS"}
        />
      </label>

      {error && <p className="text-sm text-danger">{error}</p>}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground disabled:opacity-60"
        >
          {isSubmitting ? "Submitting..." : "Submit Bug"}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-border px-3 py-2 text-sm text-muted hover:bg-surface-hover"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
