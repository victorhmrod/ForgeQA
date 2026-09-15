"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CONFIGURATION_LABELS, PLATFORM_LABELS, buildsApi, type UpdateBuildInput } from "@/lib/api/builds";
import { ApiError } from "@/lib/api/client";
import { Badge } from "@/components/badge";
import { BuildForm } from "@/components/build-form";

export default function BuildDetailPage() {
  const { projectId, buildId } = useParams<{ projectId: string; buildId: string }>();
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);

  const queryKey = ["builds", "detail", projectId, buildId];

  const { data: build, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => buildsApi.getById(projectId, buildId),
  });

  const updateMutation = useMutation({
    mutationFn: (input: UpdateBuildInput) => buildsApi.update(projectId, buildId, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      queryClient.invalidateQueries({ queryKey: ["builds", projectId] });
      setIsEditing(false);
    },
  });

  const archiveMutation = useMutation({
    mutationFn: () => buildsApi.archive(projectId, buildId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      queryClient.invalidateQueries({ queryKey: ["builds", projectId] });
    },
  });

  const restoreMutation = useMutation({
    mutationFn: () => buildsApi.restore(projectId, buildId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      queryClient.invalidateQueries({ queryKey: ["builds", projectId] });
    },
  });

  if (isLoading) {
    return <main className="px-6 py-8 text-sm text-muted">Loading...</main>;
  }

  if (isError || !build) {
    return <main className="px-6 py-8 text-sm text-danger">Build not found.</main>;
  }

  return (
    <main className="mx-auto max-w-3xl px-6 py-8">
      <Link href={`/app/projects/${projectId}/builds`} className="text-sm text-accent hover:underline">
        &larr; Back to builds
      </Link>

      <div className="mt-4 flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold">{build.name ?? build.buildNumber}</h1>
          <p className="mt-1 font-mono text-xs text-muted">
            {build.version} &middot; {build.buildNumber}
          </p>
        </div>
        <Badge tone={build.archivedAt ? "muted" : "success"}>{build.archivedAt ? "Archived" : "Active"}</Badge>
      </div>

      {isEditing ? (
        <div className="mt-6">
          <BuildForm
            mode="edit"
            initialBuild={build}
            submitLabel="Save changes"
            onCancel={() => setIsEditing(false)}
            onSubmit={async (input) => {
              try {
                await updateMutation.mutateAsync(input);
              } catch (err) {
                throw new Error(err instanceof ApiError ? err.detail ?? err.message : "Something went wrong.");
              }
            }}
          />
        </div>
      ) : (
        <div className="mt-6 flex flex-col gap-6">
          <Section title="Build">
            <Field label="Platform" value={PLATFORM_LABELS[build.platform]} />
            <Field label="Configuration" value={CONFIGURATION_LABELS[build.configuration]} />
          </Section>

          <Section title="Source">
            <Field label="Branch" value={build.source.branch ?? "—"} />
            <Field label="Commit" value={build.source.commitSha ?? "—"} mono />
          </Section>

          <Section title="Environment">
            <Field label="Engine version" value={build.engineVersion ?? "—"} />
          </Section>

          <Section title="Changelog">
            <p className="whitespace-pre-wrap text-sm text-foreground">{build.changelog ?? "No changelog provided."}</p>
          </Section>

          <Section title="Metadata">
            <Field label="Created by" value={build.createdBy.name} />
            <Field label="Created at" value={new Date(build.createdAt).toLocaleString()} />
            <Field label="Updated at" value={new Date(build.updatedAt).toLocaleString()} />
          </Section>

          <div className="flex gap-2">
            <button
              onClick={() => setIsEditing(true)}
              className="rounded-md border border-border px-3 py-2 text-sm hover:bg-surface-hover"
            >
              Edit
            </button>
            {build.archivedAt ? (
              <button
                onClick={() => restoreMutation.mutate()}
                className="rounded-md border border-border px-3 py-2 text-sm text-accent hover:bg-surface-hover"
              >
                Restore
              </button>
            ) : (
              <button
                onClick={() => archiveMutation.mutate()}
                className="rounded-md border border-border px-3 py-2 text-sm text-danger hover:bg-surface-hover"
              >
                Archive
              </button>
            )}
          </div>
        </div>
      )}
    </main>
  );
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
