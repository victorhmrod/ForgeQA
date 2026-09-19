"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { performanceApi } from "@/lib/api/performance";
import { bugsApi } from "@/lib/api/bugs";
import { formatBytes } from "@/lib/api/artifacts";
import { PerformanceLineChart } from "@/components/performance-line-chart";

function formatMetric(value: number | null, digits = 1, suffix = ""): string {
  return value === null ? "—" : `${value.toFixed(digits)}${suffix}`;
}

export default function PerformanceSessionDetailPage() {
  const { projectId, sessionId } = useParams<{ projectId: string; sessionId: string }>();

  const { data: summary, isLoading, isError } = useQuery({
    queryKey: ["performance-sessions", "detail", projectId, sessionId],
    queryFn: () => performanceApi.getSessionSummary(projectId, sessionId),
  });

  const { data: series } = useQuery({
    queryKey: ["performance-sessions", "series", projectId, sessionId],
    queryFn: () => performanceApi.getSeries(projectId, sessionId, 200),
  });

  const { data: mapBreakdown } = useQuery({
    queryKey: ["performance-sessions", "maps", projectId, sessionId],
    queryFn: () => performanceApi.getMapBreakdown(projectId, sessionId),
  });

  const { data: relatedBugs } = useQuery({
    queryKey: ["bugs", projectId, { runtimeSessionId: summary?.runtimeSessionId }],
    queryFn: () => bugsApi.list(projectId, { runtimeSessionId: summary!.runtimeSessionId, page: 1, pageSize: 10 }),
    enabled: !!summary?.runtimeSessionId,
  });

  if (isLoading) {
    return <main className="px-6 py-8 text-sm text-muted">Loading...</main>;
  }

  if (isError || !summary) {
    return <main className="px-6 py-8 text-sm text-danger">Performance session not found.</main>;
  }

  const points = series?.points ?? [];
  const hasSamples = summary.summary.sampleCount > 0;

  return (
    <main className="mx-auto max-w-4xl px-6 py-8">
      <Link href={`/app/projects/${projectId}/performance`} className="text-sm text-accent hover:underline">
        &larr; Back to performance
      </Link>

      <div className="mt-4 flex items-start justify-between gap-4">
        <div>
          <h1 className="font-mono text-lg font-semibold">{summary.runtimeSessionId}</h1>
          <p className="mt-1 text-xs text-muted">
            {summary.build.version} &middot; {summary.build.buildNumber} &middot; {summary.build.platform} / {summary.build.configuration}
          </p>
        </div>
        <Link href={`/app/projects/${projectId}/telemetry/${sessionId}`} className="text-sm text-accent hover:underline">
          View telemetry session &rarr;
        </Link>
      </div>

      {!hasSamples && (
        <p className="mt-6 text-sm text-muted">No performance samples were recorded for this session.</p>
      )}

      {hasSamples && (
        <div className="mt-6 flex flex-col gap-6">
          <section className="rounded-lg border border-border bg-surface p-4">
            <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">Summary</h2>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label="Samples" value={String(summary.summary.sampleCount)} />
              <Field label="Average FPS" value={formatMetric(summary.summary.averageFps)} />
              <Field label="Minimum FPS" value={formatMetric(summary.summary.minimumFps)} />
              <Field label="Average Frame Time" value={formatMetric(summary.summary.averageFrameTimeMs, 2, " ms")} />
              <Field label="p50 Frame Time" value={formatMetric(summary.summary.p50FrameTimeMs, 2, " ms")} />
              <Field label="p95 Frame Time" value={formatMetric(summary.summary.p95FrameTimeMs, 2, " ms")} />
              <Field label="p99 Frame Time" value={formatMetric(summary.summary.p99FrameTimeMs, 2, " ms")} />
              <Field label="Maximum Frame Time" value={formatMetric(summary.summary.maximumFrameTimeMs, 2, " ms")} />
              <Field label="Peak Memory" value={summary.summary.peakMemoryUsedBytes !== null ? formatBytes(summary.summary.peakMemoryUsedBytes) : "—"} />
            </div>
          </section>

          <section className="rounded-lg border border-border bg-surface p-4">
            <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">FPS</h2>
            <PerformanceLineChart
              points={points.map((p) => ({ timestamp: p.timestamp, averageFps: p.averageFps, minimumFps: p.minimumFps }))}
              series={[
                { key: "averageFps", label: "Average FPS", color: "#3b82f6" },
                { key: "minimumFps", label: "Minimum FPS", color: "#f97316", dashed: true },
              ]}
              referenceLines={[{ value: 30, label: "30" }, { value: 60, label: "60" }, { value: 120, label: "120" }]}
              yFormatter={(v) => v.toFixed(0)}
            />
          </section>

          <section className="rounded-lg border border-border bg-surface p-4">
            <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">Frame Time (ms)</h2>
            <PerformanceLineChart
              points={points.map((p) => ({ timestamp: p.timestamp, averageFrameTimeMs: p.averageFrameTimeMs, maximumFrameTimeMs: p.maximumFrameTimeMs }))}
              series={[
                { key: "averageFrameTimeMs", label: "Average", color: "#3b82f6" },
                { key: "maximumFrameTimeMs", label: "Max (spikes)", color: "#ef4444", dashed: true },
              ]}
              yFormatter={(v) => v.toFixed(1)}
            />
          </section>

          <section className="rounded-lg border border-border bg-surface p-4">
            <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">Memory</h2>
            <PerformanceLineChart
              points={points.map((p) => ({ timestamp: p.timestamp, memoryMb: p.averageMemoryUsedBytes !== null ? p.averageMemoryUsedBytes / (1024 * 1024) : null }))}
              series={[{ key: "memoryMb", label: "Memory Used", color: "#22c55e" }]}
              yFormatter={(v) => `${v.toFixed(0)} MB`}
            />
          </section>

          {mapBreakdown && mapBreakdown.length > 0 && (
            <section className="rounded-lg border border-border bg-surface p-4">
              <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">Map Breakdown</h2>
              <table className="w-full text-left text-sm">
                <thead className="text-xs uppercase tracking-wide text-muted">
                  <tr>
                    <th className="py-1 pr-3 font-medium">Map</th>
                    <th className="py-1 pr-3 font-medium">Samples</th>
                    <th className="py-1 pr-3 font-medium">Avg FPS</th>
                    <th className="py-1 pr-3 font-medium">p95 Frame Time</th>
                  </tr>
                </thead>
                <tbody>
                  {mapBreakdown.map((m) => (
                    <tr key={m.mapName} className="border-t border-border">
                      <td className="py-2 pr-3">{m.mapName}</td>
                      <td className="py-2 pr-3 text-muted">{m.sampleCount}</td>
                      <td className="py-2 pr-3 text-muted">{formatMetric(m.averageFps)}</td>
                      <td className="py-2 pr-3 text-muted">{formatMetric(m.p95FrameTimeMs, 2, " ms")}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}

          {relatedBugs && relatedBugs.items.length > 0 && (
            <section className="rounded-lg border border-border bg-surface p-4">
              <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted">Related Bugs</h2>
              <ul className="flex flex-col gap-1">
                {relatedBugs.items.map((bug) => (
                  <li key={bug.id}>
                    <Link href={`/app/projects/${projectId}/bugs/${bug.id}`} className="text-sm text-accent hover:underline">
                      {bug.title}
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      )}
    </main>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs text-muted">{label}</p>
      <p className="mt-0.5 text-sm">{value}</p>
    </div>
  );
}
