"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { buildsApi } from "@/lib/api/builds";
import { telemetryApi } from "@/lib/api/telemetry";
import { Badge } from "@/components/badge";

const PAGE_SIZE = 20;

function formatDuration(startedAt: string, endedAt: string | null): string {
  const start = new Date(startedAt).getTime();
  const end = endedAt ? new Date(endedAt).getTime() : Date.now();
  const totalSeconds = Math.max(0, Math.floor((end - start) / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  if (hours > 0) return `${hours}h ${minutes}m`;
  if (minutes > 0) return `${minutes}m ${seconds}s`;
  return `${seconds}s`;
}

export default function TelemetryPage() {
  const { projectId } = useParams<{ projectId: string }>();

  const [page, setPage] = useState(1);
  const [buildId, setBuildId] = useState("");
  const [activeOnly, setActiveOnly] = useState<"" | "true" | "false">("");

  const queryKey = ["telemetry-sessions", projectId, { page, buildId, activeOnly }];

  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () =>
      telemetryApi.list(projectId, {
        page,
        pageSize: PAGE_SIZE,
        buildId: buildId || undefined,
        activeOnly: activeOnly === "" ? undefined : activeOnly === "true",
      }),
  });

  const { data: builds } = useQuery({
    queryKey: ["builds", projectId, { page: 1, pageSize: 100 }],
    queryFn: () => buildsApi.list(projectId, { page: 1, pageSize: 100 }),
  });

  function resetToFirstPage<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setPage(1);
    };
  }

  return (
    <main className="mx-auto max-w-5xl px-6 py-8">
      <header className="mb-6">
        <h2 className="text-lg font-semibold">Telemetry</h2>
        <p className="mt-1 text-sm text-muted">Play sessions and events reported by ForgeQA-enabled builds.</p>
      </header>

      <div className="mb-4 flex flex-wrap items-center gap-2">
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

        <select
          value={activeOnly}
          onChange={(e) => resetToFirstPage(setActiveOnly)(e.target.value as "" | "true" | "false")}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="">All sessions</option>
          <option value="true">Active only</option>
          <option value="false">Ended only</option>
        </select>
      </div>

      {isLoading && <p className="text-sm text-muted">Loading telemetry sessions...</p>}

      {isError && <p className="text-sm text-danger">Failed to load telemetry sessions. Please try again.</p>}

      {!isLoading && !isError && data?.items.length === 0 && (
        <div className="rounded-lg border border-border bg-surface p-8 text-center">
          <p className="text-sm font-medium">No telemetry sessions yet.</p>
          <p className="mt-1 text-sm text-muted">
            Sessions started by the ForgeQA Unreal plugin&apos;s telemetry subsystem will appear here.
          </p>
        </div>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full text-left text-sm">
              <thead className="bg-surface text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3 font-medium">Session</th>
                  <th className="px-4 py-3 font-medium">Build</th>
                  <th className="px-4 py-3 font-medium">Started</th>
                  <th className="px-4 py-3 font-medium">Duration</th>
                  <th className="px-4 py-3 font-medium">Events</th>
                  <th className="px-4 py-3 font-medium">Platform</th>
                  <th className="px-4 py-3 font-medium">Last Event</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((session) => (
                  <tr key={session.id} className="border-t border-border hover:bg-surface-hover">
                    <td className="px-4 py-3">
                      <Link href={`/app/projects/${projectId}/telemetry/${session.id}`} className="text-accent hover:underline">
                        {session.runtimeSessionId.slice(0, 8)}
                      </Link>
                      <div className="mt-1">
                        <Badge tone={session.endedAt ? "muted" : "success"}>{session.endedAt ? "Ended" : "Active"}</Badge>
                      </div>
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-muted">
                      {session.build.version} &middot; {session.build.buildNumber}
                    </td>
                    <td className="px-4 py-3 text-muted">{new Date(session.startedAt).toLocaleString()}</td>
                    <td className="px-4 py-3 text-muted">{formatDuration(session.startedAt, session.endedAt)}</td>
                    <td className="px-4 py-3 text-muted">{session.eventCount}</td>
                    <td className="px-4 py-3 text-muted">{session.platform ?? "—"}</td>
                    <td className="px-4 py-3 text-muted">{session.lastEventAt ? new Date(session.lastEventAt).toLocaleString() : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex items-center justify-between text-sm text-muted">
            <span>
              Page {data.page} of {Math.max(data.totalPages, 1)} &middot; {data.totalCount} session
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
