import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithQueryClient } from "../../__tests__/test-utils";
import type { MapBreakdownItem, PerformanceSeries, PerformanceSessionSummary } from "@/lib/api/performance";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1", sessionId: "session-1" }),
}));

vi.mock("@/lib/api/performance", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/performance")>("@/lib/api/performance");
  return {
    ...actual,
    performanceApi: { list: vi.fn(), getSessionSummary: vi.fn(), getSamples: vi.fn(), getSeries: vi.fn(), getMapBreakdown: vi.fn(), getBuilds: vi.fn(), compare: vi.fn() },
  };
});

vi.mock("@/lib/api/bugs", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/bugs")>("@/lib/api/bugs");
  return {
    ...actual,
    bugsApi: { list: vi.fn(), create: vi.fn(), getById: vi.fn(), update: vi.fn(), createAttachmentDownload: vi.fn() },
  };
});

import { performanceApi } from "@/lib/api/performance";
import { bugsApi } from "@/lib/api/bugs";
import PerformanceSessionDetailPage from "../page";

const mockedGetSessionSummary = vi.mocked(performanceApi.getSessionSummary);
const mockedGetSeries = vi.mocked(performanceApi.getSeries);
const mockedGetMapBreakdown = vi.mocked(performanceApi.getMapBreakdown);
const mockedBugsList = vi.mocked(bugsApi.list);

function makeSummary(overrides: Partial<PerformanceSessionSummary> = {}): PerformanceSessionSummary {
  return {
    sessionId: "session-1",
    runtimeSessionId: "9818a0c3-20ea-45b2-ac73-1e89a1e2670e",
    build: { id: "build-1", version: "0.6.0", buildNumber: "42", platform: "WINDOWS", configuration: "DEVELOPMENT" },
    summary: {
      sampleCount: 100, averageFps: 88.7, minimumFps: 31.2, averageFrameTimeMs: 11.27, p50FrameTimeMs: 10.4,
      p95FrameTimeMs: 18.8, p99FrameTimeMs: 31.7, maximumFrameTimeMs: 62.1, averageMemoryUsedBytes: 7_902_739_824,
      peakMemoryUsedBytes: 9_020_432_384, averageGameThreadTimeMs: null, averageRenderThreadTimeMs: null, averageGpuTimeMs: null,
    },
    ...overrides,
  };
}

function emptySeries(): PerformanceSeries {
  return { points: [] };
}

beforeEach(() => {
  vi.clearAllMocks();
  mockedGetSeries.mockResolvedValue(emptySeries());
  mockedGetMapBreakdown.mockResolvedValue([]);
  mockedBugsList.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
});

describe("PerformanceSessionDetailPage", () => {
  it("renders summary metrics and the telemetry cross-link", async () => {
    mockedGetSessionSummary.mockResolvedValue(makeSummary());

    renderWithQueryClient(<PerformanceSessionDetailPage />);

    expect(await screen.findByText("9818a0c3-20ea-45b2-ac73-1e89a1e2670e")).toBeInTheDocument();
    expect(screen.getByText("88.7")).toBeInTheDocument();
    expect(screen.getByText("18.80 ms")).toBeInTheDocument();
    const link = screen.getByRole("link", { name: /View telemetry session/ });
    expect(link).toHaveAttribute("href", "/app/projects/project-1/telemetry/session-1");
  });

  it("shows the empty state when there are no performance samples", async () => {
    mockedGetSessionSummary.mockResolvedValue(makeSummary({ summary: { ...makeSummary().summary, sampleCount: 0 } }));

    renderWithQueryClient(<PerformanceSessionDetailPage />);

    expect(await screen.findByText("No performance samples were recorded for this session.")).toBeInTheDocument();
  });

  it("shows an error state when the session is not found", async () => {
    mockedGetSessionSummary.mockRejectedValue(new Error("not found"));

    renderWithQueryClient(<PerformanceSessionDetailPage />);

    expect(await screen.findByText("Performance session not found.")).toBeInTheDocument();
  });

  it("renders the map breakdown table", async () => {
    mockedGetSessionSummary.mockResolvedValue(makeSummary());
    const breakdown: MapBreakdownItem[] = [{ mapName: "Strike_Factory", sampleCount: 74, averageFps: 74.2, p95FrameTimeMs: 24.1 }];
    mockedGetMapBreakdown.mockResolvedValue(breakdown);

    renderWithQueryClient(<PerformanceSessionDetailPage />);

    expect(await screen.findByText("Strike_Factory")).toBeInTheDocument();
    expect(screen.getByText("74.2")).toBeInTheDocument();
  });

  it("renders related bugs when present", async () => {
    mockedGetSessionSummary.mockResolvedValue(makeSummary());
    mockedBugsList.mockResolvedValue({
      items: [{
        id: "bug-1", projectId: "project-1", build: null, title: "Crash after weapon fire",
        severity: "CRITICAL", status: "OPEN", source: "UNREAL_RUNTIME",
        reporter: { id: null, displayName: null }, createdAt: "2026-01-01T00:00:00Z",
      }],
      page: 1, pageSize: 10, totalCount: 1, totalPages: 1,
    });

    renderWithQueryClient(<PerformanceSessionDetailPage />);

    expect(await screen.findByText("Crash after weapon fire")).toBeInTheDocument();
  });
});
