"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  BUILD_CONFIGURATIONS,
  BUILD_PLATFORMS,
  CONFIGURATION_LABELS,
  PLATFORM_LABELS,
  buildsApi,
  type BuildConfigurationValue,
  type BuildPlatform,
  type BuildStatusFilter,
  type CreateBuildInput,
} from "@/lib/api/builds";
import { ApiError } from "@/lib/api/client";
import { Badge } from "@/components/badge";
import { BuildForm } from "@/components/build-form";

const PAGE_SIZE = 20;

export default function BuildsPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const queryClient = useQueryClient();

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [platform, setPlatform] = useState<BuildPlatform | "">("");
  const [configuration, setConfiguration] = useState<BuildConfigurationValue | "">("");
  const [status, setStatus] = useState<BuildStatusFilter>("active");
  const [isRegistering, setIsRegistering] = useState(false);

  const queryKey = ["builds", projectId, { page, search, platform, configuration, status }];

  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () =>
      buildsApi.list(projectId, {
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        platform: platform || undefined,
        configuration: configuration || undefined,
        status,
      }),
  });

  const createMutation = useMutation({
    mutationFn: (input: CreateBuildInput) => buildsApi.create(projectId, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["builds", projectId] });
      setIsRegistering(false);
    },
  });

  const archiveMutation = useMutation({
    mutationFn: (buildId: string) => buildsApi.archive(projectId, buildId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["builds", projectId] }),
  });

  const restoreMutation = useMutation({
    mutationFn: (buildId: string) => buildsApi.restore(projectId, buildId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["builds", projectId] }),
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
        <h2 className="text-lg font-semibold">Builds</h2>
        <button
          onClick={() => setIsRegistering(true)}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground"
        >
          Register Build
        </button>
      </header>

      {isRegistering && (
        <div className="mb-6">
          <BuildForm
            mode="create"
            submitLabel="Register"
            onCancel={() => setIsRegistering(false)}
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
          placeholder="Search name, version, build number, branch, commit..."
          className="min-w-[240px] flex-1 rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        />

        <select
          value={platform}
          onChange={(e) => resetToFirstPage(setPlatform)(e.target.value as BuildPlatform | "")}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="">All platforms</option>
          {BUILD_PLATFORMS.map((p) => (
            <option key={p} value={p}>
              {PLATFORM_LABELS[p]}
            </option>
          ))}
        </select>

        <select
          value={configuration}
          onChange={(e) => resetToFirstPage(setConfiguration)(e.target.value as BuildConfigurationValue | "")}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="">All configurations</option>
          {BUILD_CONFIGURATIONS.map((c) => (
            <option key={c} value={c}>
              {CONFIGURATION_LABELS[c]}
            </option>
          ))}
        </select>

        <select
          value={status}
          onChange={(e) => resetToFirstPage(setStatus)(e.target.value as BuildStatusFilter)}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="active">Active</option>
          <option value="archived">Archived</option>
          <option value="all">All</option>
        </select>
      </div>

      {isLoading && <p className="text-sm text-muted">Loading builds...</p>}

      {isError && <p className="text-sm text-danger">Failed to load builds. Please try again.</p>}

      {!isLoading && !isError && data?.items.length === 0 && (
        <div className="rounded-lg border border-border bg-surface p-8 text-center">
          <p className="text-sm font-medium">No builds registered yet.</p>
          <p className="mt-1 text-sm text-muted">
            Register a build to associate future test sessions, bug reports, crashes, and telemetry with an
            exact version of your project.
          </p>
        </div>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full text-left text-sm">
              <thead className="bg-surface text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3 font-medium">Build</th>
                  <th className="px-4 py-3 font-medium">Version</th>
                  <th className="px-4 py-3 font-medium">Build number</th>
                  <th className="px-4 py-3 font-medium">Platform</th>
                  <th className="px-4 py-3 font-medium">Configuration</th>
                  <th className="px-4 py-3 font-medium">Branch</th>
                  <th className="px-4 py-3 font-medium">Commit</th>
                  <th className="px-4 py-3 font-medium">Created</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium" />
                </tr>
              </thead>
              <tbody>
                {data.items.map((build) => (
                  <tr key={build.id} className="border-t border-border hover:bg-surface-hover">
                    <td className="px-4 py-3">
                      <Link href={`/app/projects/${projectId}/builds/${build.id}`} className="text-accent hover:underline">
                        {build.name ?? build.buildNumber}
                      </Link>
                    </td>
                    <td className="px-4 py-3 font-mono text-xs">{build.version}</td>
                    <td className="px-4 py-3 font-mono text-xs">{build.buildNumber}</td>
                    <td className="px-4 py-3">{PLATFORM_LABELS[build.platform]}</td>
                    <td className="px-4 py-3">{CONFIGURATION_LABELS[build.configuration]}</td>
                    <td className="px-4 py-3 text-muted">{build.source.branch ?? "—"}</td>
                    <td className="px-4 py-3 font-mono text-xs text-muted">{build.source.commitSha ?? "—"}</td>
                    <td className="px-4 py-3 text-muted">{new Date(build.createdAt).toLocaleDateString()}</td>
                    <td className="px-4 py-3">
                      <Badge tone={build.archivedAt ? "muted" : "success"}>
                        {build.archivedAt ? "Archived" : "Active"}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-right">
                      {build.archivedAt ? (
                        <button
                          onClick={() => restoreMutation.mutate(build.id)}
                          className="text-xs text-accent hover:underline"
                        >
                          Restore
                        </button>
                      ) : (
                        <button
                          onClick={() => archiveMutation.mutate(build.id)}
                          className="text-xs text-muted hover:text-danger hover:underline"
                        >
                          Archive
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex items-center justify-between text-sm text-muted">
            <span>
              Page {data.page} of {Math.max(data.totalPages, 1)} &middot; {data.totalCount} build
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
