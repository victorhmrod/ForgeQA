"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  BUG_SEVERITIES,
  BUG_STATUSES,
  SEVERITY_LABELS,
  SOURCE_LABELS,
  STATUS_LABELS,
  bugsApi,
  type Bug,
  type BugSeverity,
  type BugStatusValue,
  type UpdateBugInput,
} from "@/lib/api/bugs";
import { ApiError } from "@/lib/api/client";
import { telemetryApi } from "@/lib/api/telemetry";
import { SeverityBadge, StatusBadge } from "@/components/bug-badges";
import { AttachmentPreview } from "@/components/attachment-preview";

export default function BugDetailPage() {
  const { projectId, bugId } = useParams<{ projectId: string; bugId: string }>();
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);

  const queryKey = ["bugs", "detail", projectId, bugId];

  const { data: bug, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => bugsApi.getById(projectId, bugId),
  });

  const updateMutation = useMutation({
    mutationFn: (input: UpdateBugInput) => bugsApi.update(projectId, bugId, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      queryClient.invalidateQueries({ queryKey: ["bugs", projectId] });
      setIsEditing(false);
    },
  });

  // Correlated via ProjectId + RuntimeSessionId only — no stored FK, no duplicated telemetry data
  // on the bug itself. See docs/telemetry.md.
  const { data: telemetryMatch } = useQuery({
    queryKey: ["telemetry-sessions", projectId, { runtimeSessionId: bug?.runtimeSessionId }],
    queryFn: () => telemetryApi.list(projectId, { runtimeSessionId: bug!.runtimeSessionId!, page: 1, pageSize: 1 }),
    enabled: !!bug?.runtimeSessionId,
  });
  const telemetrySessionId = telemetryMatch?.items[0]?.id;

  if (isLoading) {
    return <main className="px-6 py-8 text-sm text-muted">Loading...</main>;
  }

  if (isError || !bug) {
    return <main className="px-6 py-8 text-sm text-danger">Bug report not found.</main>;
  }

  return (
    <main className="mx-auto max-w-3xl px-6 py-8">
      <Link href={`/app/projects/${projectId}/bugs`} className="text-sm text-accent hover:underline">
        &larr; Back to bugs
      </Link>

      <div className="mt-4 flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">{bug.title}</h1>
          <p className="mt-1 text-xs text-muted">Reported {new Date(bug.createdAt).toLocaleString()}</p>
        </div>
        <div className="flex shrink-0 gap-2">
          <SeverityBadge severity={bug.severity} />
          <StatusBadge status={bug.status} />
        </div>
      </div>

      {isEditing ? (
        <BugEditForm
          bug={bug}
          isSubmitting={updateMutation.isPending}
          submitError={
            updateMutation.error instanceof ApiError
              ? updateMutation.error.detail ?? updateMutation.error.message
              : updateMutation.isError
                ? "Something went wrong."
                : null
          }
          onCancel={() => setIsEditing(false)}
          onSubmit={(input) => updateMutation.mutate(input)}
        />
      ) : (
        <div className="mt-6 flex flex-col gap-6">
          <Section title="Bug">
            <p className="whitespace-pre-wrap text-sm text-foreground sm:col-span-2">
              {bug.description ?? "No description provided."}
            </p>
          </Section>

          <Section title="Reproduction">
            <p className="whitespace-pre-wrap text-sm text-foreground sm:col-span-2">
              {bug.reproductionSteps ?? "No reproduction steps provided."}
            </p>
          </Section>

          {bug.build && (
            <Section title="Build">
              <Field label="Version" value={`${bug.build.version} · ${bug.build.buildNumber}`} />
              <Field label="Platform / Configuration" value={`${bug.build.platform} / ${bug.build.configuration}`} />
              <div className="sm:col-span-2">
                <Link
                  href={`/app/projects/${projectId}/builds/${bug.build.id}`}
                  className="text-sm text-accent hover:underline"
                >
                  View build detail &rarr;
                </Link>
              </div>
            </Section>
          )}

          {hasEnvironmentData(bug.environment) && (
            <Section title="Environment">
              {bug.environment.mapName && <Field label="Map" value={bug.environment.mapName} />}
              {bug.environment.gameMode && <Field label="Game mode" value={bug.environment.gameMode} />}
              {bug.environment.platform && <Field label="Platform" value={bug.environment.platform} />}
              {bug.environment.engineVersion && <Field label="Engine version" value={bug.environment.engineVersion} />}
              {bug.environment.osVersion && <Field label="OS" value={bug.environment.osVersion} />}
              {bug.environment.cpu && <Field label="CPU" value={bug.environment.cpu} />}
              {bug.environment.gpu && <Field label="GPU" value={bug.environment.gpu} />}
              {bug.environment.locale && <Field label="Locale" value={bug.environment.locale} />}
            </Section>
          )}

          {bug.attachments.length > 0 && (
            <Section title="Attachments">
              <div className="sm:col-span-2 flex flex-wrap gap-3">
                {bug.attachments.map((attachment) => (
                  <AttachmentPreview key={attachment.id} projectId={projectId} bugId={bugId} attachment={attachment} />
                ))}
              </div>
            </Section>
          )}

          <Section title="Metadata">
            <Field label="Source" value={SOURCE_LABELS[bug.source]} />
            <Field label="Reporter" value={bug.reporter.displayName ?? "Anonymous / runtime"} />
            <Field label="Created at" value={new Date(bug.createdAt).toLocaleString()} />
            <Field label="Updated at" value={new Date(bug.updatedAt).toLocaleString()} />
            {bug.resolvedAt && <Field label="Resolved at" value={new Date(bug.resolvedAt).toLocaleString()} />}
            {bug.closedAt && <Field label="Closed at" value={new Date(bug.closedAt).toLocaleString()} />}
            {bug.runtimeSessionId && <Field label="Runtime session" value={bug.runtimeSessionId} mono />}
          </Section>

          {telemetrySessionId && (
            <div>
              <Link
                href={`/app/projects/${projectId}/telemetry/${telemetrySessionId}`}
                className="text-sm text-accent hover:underline"
              >
                View telemetry session &rarr;
              </Link>
            </div>
          )}

          <div>
            <button
              onClick={() => setIsEditing(true)}
              className="rounded-md border border-border px-3 py-2 text-sm hover:bg-surface-hover"
            >
              Edit
            </button>
          </div>
        </div>
      )}
    </main>
  );
}

function BugEditForm({
  bug,
  isSubmitting,
  submitError,
  onCancel,
  onSubmit,
}: {
  bug: Bug;
  isSubmitting: boolean;
  submitError: string | null;
  onCancel: () => void;
  onSubmit: (input: UpdateBugInput) => void;
}) {
  const [title, setTitle] = useState(bug.title);
  const [description, setDescription] = useState(bug.description ?? "");
  const [reproductionSteps, setReproductionSteps] = useState(bug.reproductionSteps ?? "");
  const [severity, setSeverity] = useState<BugSeverity>(bug.severity);
  const [status, setStatus] = useState<BugStatusValue>(bug.status);
  const [validationError, setValidationError] = useState<string | null>(null);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) {
      setValidationError("Title is required.");
      return;
    }
    setValidationError(null);
    onSubmit({ title, description: description || undefined, reproductionSteps: reproductionSteps || undefined, severity, status });
  }

  return (
    <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-4 rounded-lg border border-border bg-surface p-4">
      <label className="flex flex-col gap-1.5 text-sm">
        <span className="text-muted font-medium">Title</span>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        />
      </label>

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted font-medium">Severity</span>
          <select
            value={severity}
            onChange={(e) => setSeverity(e.target.value as BugSeverity)}
            className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
          >
            {BUG_SEVERITIES.map((s) => (
              <option key={s} value={s}>
                {SEVERITY_LABELS[s]}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted font-medium">Status</span>
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as BugStatusValue)}
            className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
          >
            {BUG_STATUSES.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABELS[s]}
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
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        />
      </label>

      <label className="flex flex-col gap-1.5 text-sm">
        <span className="text-muted font-medium">Steps to reproduce</span>
        <textarea
          value={reproductionSteps}
          onChange={(e) => setReproductionSteps(e.target.value)}
          rows={3}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        />
      </label>

      {(validationError || submitError) && <p className="text-sm text-danger">{validationError ?? submitError}</p>}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground disabled:opacity-60"
        >
          {isSubmitting ? "Saving..." : "Save changes"}
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

function hasEnvironmentData(environment: {
  mapName: string | null;
  gameMode: string | null;
  platform: string | null;
  engineVersion: string | null;
  osVersion: string | null;
  cpu: string | null;
  gpu: string | null;
  locale: string | null;
}) {
  return Object.values(environment).some((value) => value !== null && value !== "");
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg border border-border bg-surface p-4">
      <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">{title}</h2>
      <div className="grid gap-3 sm:grid-cols-2">{children}</div>
    </section>
  );
}

function Field({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <p className="text-xs text-muted">{label}</p>
      <p className={`mt-0.5 text-sm ${mono ? "font-mono" : ""}`}>{value}</p>
    </div>
  );
}
