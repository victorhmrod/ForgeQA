"use client";

import { useState } from "react";
import {
  BUILD_CONFIGURATIONS,
  BUILD_PLATFORMS,
  CONFIGURATION_LABELS,
  PLATFORM_LABELS,
  type Build,
  type BuildConfigurationValue,
  type BuildPlatform,
  type CreateBuildInput,
  type UpdateBuildInput,
} from "@/lib/api/builds";
import { FormField } from "@/components/form-field";

interface BuildFormProps {
  mode: "create" | "edit";
  initialBuild?: Build;
  onSubmit: (input: CreateBuildInput & UpdateBuildInput) => Promise<void>;
  onCancel: () => void;
  submitLabel: string;
}

export function BuildForm({ mode, initialBuild, onSubmit, onCancel, submitLabel }: BuildFormProps) {
  const [name, setName] = useState(initialBuild?.name ?? "");
  const [version, setVersion] = useState(initialBuild?.version ?? "");
  const [buildNumber, setBuildNumber] = useState(initialBuild?.buildNumber ?? "");
  const [platform, setPlatform] = useState<BuildPlatform>(initialBuild?.platform ?? "WINDOWS");
  const [configuration, setConfiguration] = useState<BuildConfigurationValue>(
    initialBuild?.configuration ?? "DEVELOPMENT",
  );
  const [branch, setBranch] = useState(initialBuild?.source.branch ?? "");
  const [commitSha, setCommitSha] = useState(initialBuild?.source.commitSha ?? "");
  const [engineVersion, setEngineVersion] = useState(initialBuild?.engineVersion ?? "");
  const [changelog, setChangelog] = useState(initialBuild?.changelog ?? "");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!version.trim()) {
      setError("Version is required.");
      return;
    }
    if (mode === "create" && !buildNumber.trim()) {
      setError("Build number is required.");
      return;
    }

    setIsSubmitting(true);
    try {
      await onSubmit({
        name: name || undefined,
        version,
        buildNumber,
        platform,
        configuration,
        branch: branch || undefined,
        commitSha: commitSha || undefined,
        engineVersion: engineVersion || undefined,
        changelog: changelog || undefined,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Name" value={name} onChange={(e) => setName(e.target.value)} placeholder="QA Candidate" />
        <FormField label="Version" required value={version} onChange={(e) => setVersion(e.target.value)} placeholder="0.4.2" />

        <FormField
          label="Build number"
          required
          value={buildNumber}
          onChange={(e) => setBuildNumber(e.target.value)}
          placeholder="1842"
          disabled={mode === "edit"}
        />

        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted font-medium">Platform</span>
          <select
            value={platform}
            disabled={mode === "edit"}
            onChange={(e) => setPlatform(e.target.value as BuildPlatform)}
            className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-accent focus:ring-1 focus:ring-accent disabled:opacity-60"
          >
            {BUILD_PLATFORMS.map((p) => (
              <option key={p} value={p}>
                {PLATFORM_LABELS[p]}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted font-medium">Configuration</span>
          <select
            value={configuration}
            disabled={mode === "edit"}
            onChange={(e) => setConfiguration(e.target.value as BuildConfigurationValue)}
            className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-accent focus:ring-1 focus:ring-accent disabled:opacity-60"
          >
            {BUILD_CONFIGURATIONS.map((c) => (
              <option key={c} value={c}>
                {CONFIGURATION_LABELS[c]}
              </option>
            ))}
          </select>
        </label>

        <FormField label="Branch" value={branch} onChange={(e) => setBranch(e.target.value)} placeholder="main" />
        <FormField
          label="Commit SHA"
          value={commitSha}
          onChange={(e) => setCommitSha(e.target.value)}
          placeholder="a941de3"
        />
        <FormField
          label="Engine version"
          value={engineVersion}
          onChange={(e) => setEngineVersion(e.target.value)}
          placeholder="UE 5.8.2"
        />
      </div>

      <label className="flex flex-col gap-1.5 text-sm">
        <span className="text-muted font-medium">Changelog</span>
        <textarea
          value={changelog}
          onChange={(e) => setChangelog(e.target.value)}
          rows={4}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-accent focus:ring-1 focus:ring-accent"
          placeholder={"- Added new inventory system\n- Fixed replicated movement jitter"}
        />
      </label>

      {error && <p className="text-sm text-danger">{error}</p>}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground disabled:opacity-60"
        >
          {isSubmitting ? "Saving..." : submitLabel}
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
