"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { buildsApi } from "@/lib/api/builds";
import {
  BUG_SEVERITIES,
  BUG_STATUSES,
  SEVERITY_LABELS,
  STATUS_LABELS,
  bugsApi,
  type BugSeverity,
  type BugStatusValue,
  type CreateBugInput,
} from "@/lib/api/bugs";
import { ApiError } from "@/lib/api/client";
import { SeverityBadge, StatusBadge } from "@/components/bug-badges";
import { CreateBugForm } from "@/components/create-bug-form";

const PAGE_SIZE = 20;

export default function BugsPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const queryClient = useQueryClient();

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [severity, setSeverity] = useState<BugSeverity | "">("");
  const [status, setStatus] = useState<BugStatusValue | "">("");
  const [buildId, setBuildId] = useState("");
  const [isCreating, setIsCreating] = useState(false);

  const queryKey = ["bugs", projectId, { page, search, severity, status, buildId }];

  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () =>
      bugsApi.list(projectId, {
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        severity: severity || undefined,
        status: status || undefined,
        buildId: buildId || undefined,
      }),
  });

  const { data: builds } = useQuery({
    queryKey: ["builds", projectId, { page: 1, pageSize: 100 }],
    queryFn: () => buildsApi.list(projectId, { page: 1, pageSize: 100 }),
  });

  const createMutation = useMutation({
    mutationFn: (input: CreateBugInput) => bugsApi.create(projectId, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["bugs", projectId] });
      setIsCreating(false);
    },
  });

  function resetToFirstPage<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-8">
      <header className="mb-6 flex items-center justify-between">
        <h2 className="text-lg font-semibold">Bugs</h2>
        <button
          onClick={() => setIsCreating(true)}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground"
        >
          Create Bug
        </button>
      </header>

      {isCreating && (
        <div className="mb-6">
          <CreateBugForm
            projectId={projectId}
            onCancel={() => setIsCreating(false)}
            onSubmit={async (input) => {
              try {
                await createMutation.mutateAsync(input);
              } catch (err) {
                throw new Error(err instanceof ApiError ? err.detail ?? err.message : "Something went wrong.");
              }
            }}
          />
        </div>
      )}

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <input
          value={search}
          onChange={(e) => resetToFirstPage(setSearch)(e.target.value)}
          placeholder="Search title, description..."
          className="min-w-[240px] flex-1 rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        />

        <select
          value={severity}
          onChange={(e) => resetToFirstPage(setSeverity)(e.target.value as BugSeverity | "")}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="">All severities</option>
          {BUG_SEVERITIES.map((s) => (
            <option key={s} value={s}>
              {SEVERITY_LABELS[s]}
            </option>
          ))}
        </select>

        <select
          value={status}
          onChange={(e) => resetToFirstPage(setStatus)(e.target.value as BugStatusValue | "")}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="">All statuses</option>
          {BUG_STATUSES.map((s) => (
            <option key={s} value={s}>
              {STATUS_LABELS[s]}
            </option>
          ))}
        </select>

        <select
          value={buildId}
          onChange={(e) => resetToFirstPage(setBuildId)(e.target.value)}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="">All builds</option>
          {builds?.items.map((build) => (
            <option key={build.id} value={build.id}>
              {build.name ?? `${build.version} (${build.buildNumber})`}
            </option>
          ))}
        </select>
      </div>

      {isLoading && <p className="text-sm text-muted">Loading bugs...</p>}

      {isError && <p className="text-sm text-danger">Failed to load bugs. Please try again.</p>}

      {!isLoading && !isError && data?.items.length === 0 && (
        <div className="rounded-lg border border-border bg-surface p-8 text-center">
          <p className="text-sm font-medium">No bugs reported yet.</p>
          <p className="mt-1 text-sm text-muted">
            Bugs submitted from the ForgeQA Unreal plugin or created manually will appear here.
          </p>
        </div>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full text-left text-sm">
              <thead className="bg-surface text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3 font-medium">Title</th>
                  <th className="px-4 py-3 font-medium">Severity</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Build</th>
                  <th className="px-4 py-3 font-medium">Source</th>
                  <th className="px-4 py-3 font-medium">Reporter</th>
                  <th className="px-4 py-3 font-medium">Created</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((bug) => (
                  <tr key={bug.id} className="border-t border-border hover:bg-surface-hover">
                    <td className="px-4 py-3">
                      <Link href={`/app/projects/${projectId}/bugs/${bug.id}`} className="text-accent hover:underline">
                        {bug.title}
                      </Link>
                    </td>
                    <td className="px-4 py-3">
                      <SeverityBadge severity={bug.severity} />
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={bug.status} />
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-muted">
                      {bug.build ? `${bug.build.version} · ${bug.build.buildNumber}` : "—"}
                    </td>
                    <td className="px-4 py-3 text-muted">{bug.source}</td>
                    <td className="px-4 py-3 text-muted">{bug.reporter.displayName ?? "—"}</td>
                    <td className="px-4 py-3 text-muted">{new Date(bug.createdAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex items-center justify-between text-sm text-muted">
            <span>
              Page {data.page} of {Math.max(data.totalPages, 1)} &middot; {data.totalCount} bug
              {data.totalCount === 1 ? "" : "s"}
            </span>
            <div className="flex gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="rounded-md border border-border px-3 py-1.5 disabled:opacity-40"
              >
                Previous
              </button>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={page >= data.totalPages}
                className="rounded-md border border-border px-3 py-1.5 disabled:opacity-40"
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}
    </main>
  );
}
