"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { buildsApi } from "@/lib/api/builds";
import { performanceApi } from "@/lib/api/performance";
import { formatBytes } from "@/lib/api/artifacts";

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

function formatMetric(value: number | null, digits = 1): string {
  return value === null ? "—" : value.toFixed(digits);
}

const DATE_PRESETS = [
  { label: "All time", days: null },
  { label: "Last 24h", days: 1 },
  { label: "Last 7 days", days: 7 },
  { label: "Last 30 days", days: 30 },
] as const;

export default function PerformancePage() {
  const { projectId } = useParams<{ projectId: string }>();

  const [page, setPage] = useState(1);
  const [buildId, setBuildId] = useState("");
  const [platform, setPlatform] = useState("");
  const [presetDays, setPresetDays] = useState<number | null>(null);

  const queryKey = ["performance-sessions", projectId, { page, buildId, platform, presetDays }];

  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () =>
      performanceApi.list(projectId, {
        page,
        pageSize: PAGE_SIZE,
        buildId: buildId || undefined,
        platform: platform || undefined,
        from: presetDays ? new Date(Date.now() - presetDays * 24 * 60 * 60 * 1000).toISOString() : undefined,
      }),
  });

  const { data: builds } = useQuery({
    queryKey: ["builds", projectId, { page: 1, pageSize: 100 }],
    queryFn: () => buildsApi.list(projectId, { page: 1, pageSize: 100 }),
  });

  const { data: buildSummaries } = useQuery({
    queryKey: ["performance-builds", projectId],
    queryFn: () => performanceApi.getBuilds(projectId),
  });

  const [compareA, setCompareA] = useState("");
  const [compareB, setCompareB] = useState("");
  const { data: compareResult, refetch: runCompare, isFetching: isComparing } = useQuery({
    queryKey: ["performance-compare", projectId, compareA, compareB],
    queryFn: () => performanceApi.compare(projectId, compareA, compareB),
    enabled: false,
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
        <h2 className="text-lg font-semibold">Performance</h2>
        <p className="mt-1 text-sm text-muted">How are recent Builds performing?</p>
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
          value={platform}
          onChange={(e) => resetToFirstPage(setPlatform)(e.target.value)}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          <option value="">All platforms</option>
          <option value="WINDOWS">Windows</option>
          <option value="LINUX">Linux</option>
          <option value="MACOS">macOS</option>
        </select>

        <select
          value={presetDays ?? ""}
          onChange={(e) => resetToFirstPage(setPresetDays)(e.target.value ? Number(e.target.value) : null)}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent focus:ring-1 focus:ring-accent"
        >
          {DATE_PRESETS.map((preset) => (
            <option key={preset.label} value={preset.days ?? ""}>
              {preset.label}
            </option>
          ))}
        </select>
      </div>

      {isLoading && <p className="text-sm text-muted">Loading performance sessions...</p>}
      {isError && <p className="text-sm text-danger">Failed to load performance sessions. Please try again.</p>}

      {!isLoading && !isError && data?.items.length === 0 && (
        <div className="rounded-lg border border-border bg-surface p-8 text-center">
          <p className="text-sm font-medium">No performance data yet.</p>
          <p className="mt-1 text-sm text-muted">Performance samples collected by the ForgeQA Unreal plugin will appear here.</p>
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
                  <th className="px-4 py-3 font-medium">Samples</th>
                  <th className="px-4 py-3 font-medium">Avg FPS</th>
                  <th className="px-4 py-3 font-medium">Min FPS</th>
                  <th className="px-4 py-3 font-medium">p95 Frame Time</th>
                  <th className="px-4 py-3 font-medium">Peak Memory</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((session) => (
                  <tr key={session.sessionId} className="border-t border-border hover:bg-surface-hover">
                    <td className="px-4 py-3">
                      <Link href={`/app/projects/${projectId}/performance/${session.sessionId}`} className="text-accent hover:underline">
                        {session.runtimeSessionId.slice(0, 8)}
                      </Link>
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-muted">{session.build.version} &middot; {session.build.buildNumber}</td>
                    <td className="px-4 py-3 text-muted">{new Date(session.startedAt).toLocaleString()}</td>
                    <td className="px-4 py-3 text-muted">{formatDuration(session.startedAt, session.endedAt)}</td>
                    <td className="px-4 py-3 text-muted">{session.summary.sampleCount}</td>
                    <td className="px-4 py-3 text-muted">{formatMetric(session.summary.averageFps)}</td>
                    <td className="px-4 py-3 text-muted">{formatMetric(session.summary.minimumFps)}</td>
                    <td className="px-4 py-3 text-muted">{formatMetric(session.summary.p95FrameTimeMs)} ms</td>
                    <td className="px-4 py-3 text-muted">
                      {session.summary.peakMemoryUsedBytes !== null ? formatBytes(session.summary.peakMemoryUsedBytes) : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex items-center justify-between text-sm text-muted">
            <span>
              Page {data.page} of {Math.max(data.totalPages, 1)} &middot; {data.totalCount} session{data.totalCount === 1 ? "" : "s"}
            </span>
            <div className="flex gap-2">
              <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1.5 disabled:opacity-40">
                Previous
              </button>
              <button onClick={() => setPage((p) => p + 1)} disabled={page >= data.totalPages} className="rounded-md border border-border px-3 py-1.5 disabled:opacity-40">
                Next
              </button>
            </div>
          </div>
        </>
      )}

      {buildSummaries && buildSummaries.length > 0 && (
        <section className="mt-10">
          <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted">Build Performance</h3>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full text-left text-sm">
              <thead className="bg-surface text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3 font-medium">Build</th>
                  <th className="px-4 py-3 font-medium">Sessions</th>
                  <th className="px-4 py-3 font-medium">Samples</th>
                  <th className="px-4 py-3 font-medium">Avg FPS</th>
                  <th className="px-4 py-3 font-medium">p95 Frame Time</th>
                  <th className="px-4 py-3 font-medium">Peak Memory</th>
                </tr>
              </thead>
              <tbody>
                {buildSummaries.map((b) => (
                  <tr key={b.buildId} className="border-t border-border">
                    <td className="px-4 py-3 font-mono text-xs">
                      {b.version} &middot; {b.buildNumber} {b.isArchived && <span className="text-muted">(archived)</span>}
                    </td>
                    <td className="px-4 py-3 text-muted">{b.sessionCount}</td>
                    <td className="px-4 py-3 text-muted">{b.summary.sampleCount}</td>
                    <td className="px-4 py-3 text-muted">{formatMetric(b.summary.averageFps)}</td>
                    <td className="px-4 py-3 text-muted">{formatMetric(b.summary.p95FrameTimeMs)} ms</td>
                    <td className="px-4 py-3 text-muted">{b.summary.peakMemoryUsedBytes !== null ? formatBytes(b.summary.peakMemoryUsedBytes) : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {buildSummaries && buildSummaries.length > 1 && (
        <section className="mt-10">
          <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted">Compare Builds</h3>
          <div className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-muted">Build A</span>
              <select value={compareA} onChange={(e) => setCompareA(e.target.value)} className="rounded-md border border-border bg-surface px-3 py-2 text-sm">
                <option value="">Select a build</option>
                {buildSummaries.map((b) => (
                  <option key={b.buildId} value={b.buildId}>
                    {b.version} &middot; {b.buildNumber}
                  </option>
                ))}
              </select>
            </label>
            <span className="pb-2 text-sm text-muted">vs</span>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-muted">Build B</span>
              <select value={compareB} onChange={(e) => setCompareB(e.target.value)} className="rounded-md border border-border bg-surface px-3 py-2 text-sm">
                <option value="">Select a build</option>
                {buildSummaries.map((b) => (
                  <option key={b.buildId} value={b.buildId}>
                    {b.version} &middot; {b.buildNumber}
                  </option>
                ))}
              </select>
            </label>
            <button
              onClick={() => runCompare()}
              disabled={!compareA || !compareB || isComparing}
              className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground disabled:opacity-60"
            >
              {isComparing ? "Comparing..." : "Compare"}
            </button>
          </div>

          {compareResult && (
            <div className="mt-4 overflow-x-auto rounded-lg border border-border">
              <table className="w-full text-left text-sm">
                <thead className="bg-surface text-xs uppercase tracking-wide text-muted">
                  <tr>
                    <th className="px-4 py-3 font-medium">Metric</th>
                    <th className="px-4 py-3 font-medium">{compareResult.buildA.version} · {compareResult.buildA.buildNumber}</th>
                    <th className="px-4 py-3 font-medium">{compareResult.buildB.version} · {compareResult.buildB.buildNumber}</th>
                    <th className="px-4 py-3 font-medium">Delta</th>
                  </tr>
                </thead>
                <tbody>
                  {compareResult.metrics.map((m) => (
                    <tr key={m.metric} className="border-t border-border">
                      <td className="px-4 py-3">{m.metric}</td>
                      <td className="px-4 py-3 text-muted">{formatMetric(m.buildAValue, 2)}</td>
                      <td className="px-4 py-3 text-muted">{formatMetric(m.buildBValue, 2)}</td>
                      <td className="px-4 py-3 text-muted">
                        {m.delta === null
                          ? "—"
                          : `${m.delta >= 0 ? "+" : ""}${m.delta.toFixed(2)}${m.deltaPercent !== null ? ` (${m.deltaPercent >= 0 ? "+" : ""}${m.deltaPercent.toFixed(1)}%)` : ""}`}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}
    </main>
  );
}
