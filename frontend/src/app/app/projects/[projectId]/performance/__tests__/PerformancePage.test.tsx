import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "./test-utils";
import type { BuildPerformanceListItem, PerformanceSessionListItem, PerformanceSummary } from "@/lib/api/performance";
import type { PagedResult } from "@/lib/api/pagination";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1" }),
}));

vi.mock("@/lib/api/performance", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/performance")>("@/lib/api/performance");
  return {
    ...actual,
    performanceApi: { list: vi.fn(), getSessionSummary: vi.fn(), getSamples: vi.fn(), getSeries: vi.fn(), getMapBreakdown: vi.fn(), getBuilds: vi.fn(), compare: vi.fn() },
  };
});

vi.mock("@/lib/api/builds", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/builds")>("@/lib/api/builds");
  return {
    ...actual,
    buildsApi: { list: vi.fn(), create: vi.fn(), getById: vi.fn(), update: vi.fn(), archive: vi.fn(), restore: vi.fn() },
  };
});

import { performanceApi } from "@/lib/api/performance";
import { buildsApi } from "@/lib/api/builds";
import PerformancePage from "../page";

const mockedList = vi.mocked(performanceApi.list);
const mockedGetBuilds = vi.mocked(performanceApi.getBuilds);
const mockedCompare = vi.mocked(performanceApi.compare);
const mockedBuildsList = vi.mocked(buildsApi.list);

function emptySummary(overrides: Partial<PerformanceSummary> = {}): PerformanceSummary {
  return {
    sampleCount: 0, averageFps: null, minimumFps: null, averageFrameTimeMs: null, p50FrameTimeMs: null,
    p95FrameTimeMs: null, p99FrameTimeMs: null, maximumFrameTimeMs: null, averageMemoryUsedBytes: null,
    peakMemoryUsedBytes: null, averageGameThreadTimeMs: null, averageRenderThreadTimeMs: null, averageGpuTimeMs: null,
    ...overrides,
  };
}

function makeSession(overrides: Partial<PerformanceSessionListItem> = {}): PerformanceSessionListItem {
  return {
    sessionId: "session-1",
    runtimeSessionId: "9818a0c3-20ea-45b2-ac73-1e89a1e2670e",
    build: { id: "build-1", version: "0.6.0", buildNumber: "42", platform: "WINDOWS", configuration: "DEVELOPMENT" },
    startedAt: "2026-01-01T00:00:00Z",
    endedAt: null,
    platform: "WINDOWS",
    summary: emptySummary({ sampleCount: 120, averageFps: 88.5, minimumFps: 31.2, p95FrameTimeMs: 18.8, peakMemoryUsedBytes: 9_020_432_384 }),
    ...overrides,
  };
}

function pagedResult(items: PerformanceSessionListItem[]): PagedResult<PerformanceSessionListItem> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

function makeBuild(overrides: Partial<BuildPerformanceListItem> = {}): BuildPerformanceListItem {
  return {
    buildId: "build-1", version: "0.6.0", buildNumber: "42", platform: "WINDOWS", configuration: "DEVELOPMENT",
    isArchived: false, sessionCount: 3, summary: emptySummary({ sampleCount: 300, averageFps: 80 }),
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  mockedBuildsList.mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 });
  mockedGetBuilds.mockResolvedValue([]);
});

describe("PerformancePage", () => {
  it("renders session rows with summary metrics", async () => {
    mockedList.mockResolvedValue(pagedResult([makeSession()]));

    renderWithQueryClient(<PerformancePage />);

    expect(await screen.findByText("9818a0c3")).toBeInTheDocument();
    expect(screen.getByText("88.5")).toBeInTheDocument();
    expect(screen.getByText("31.2")).toBeInTheDocument();
  });

  it("shows the empty state when there is no performance data", async () => {
    mockedList.mockResolvedValue(pagedResult([]));

    renderWithQueryClient(<PerformancePage />);

    expect(await screen.findByText("No performance data yet.")).toBeInTheDocument();
  });

  it("shows an error state when the list request fails", async () => {
    mockedList.mockRejectedValue(new Error("network error"));

    renderWithQueryClient(<PerformancePage />);

    expect(await screen.findByText(/Failed to load performance sessions/i)).toBeInTheDocument();
  });

  it("requests sessions filtered by the selected build", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    mockedBuildsList.mockResolvedValue({
      items: [{
        id: "build-1", projectId: "project-1", name: null, version: "0.6.0", buildNumber: "42",
        platform: "WINDOWS", configuration: "DEVELOPMENT", source: { branch: null, commitSha: null },
        engineVersion: null, changelog: null, archivedAt: null,
        createdBy: { id: "u", name: "Victor" }, createdAt: "2026-01-01T00:00:00Z", updatedAt: "2026-01-01T00:00:00Z",
      }],
      page: 1, pageSize: 100, totalCount: 1, totalPages: 1,
    });
    const user = userEvent.setup();

    renderWithQueryClient(<PerformancePage />);
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    const buildSelect = await screen.findByDisplayValue("All builds");
    await user.selectOptions(buildSelect, "build-1");

    await waitFor(() => expect(mockedList).toHaveBeenLastCalledWith("project-1", expect.objectContaining({ buildId: "build-1" })));
  });

  it("renders Build performance summaries", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    mockedGetBuilds.mockResolvedValue([makeBuild()]);

    renderWithQueryClient(<PerformancePage />);

    expect(await screen.findByText("Build Performance")).toBeInTheDocument();
    expect(screen.getByText("80.0")).toBeInTheDocument();
  });

  it("compares two builds and renders deltas", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    mockedGetBuilds.mockResolvedValue([
      makeBuild({ buildId: "build-1", buildNumber: "1842" }),
      makeBuild({ buildId: "build-2", buildNumber: "1843" }),
    ]);
    mockedCompare.mockResolvedValue({
      buildA: makeBuild({ buildId: "build-1", buildNumber: "1842", summary: emptySummary({ averageFps: 60 }) }),
      buildB: makeBuild({ buildId: "build-2", buildNumber: "1843", summary: emptySummary({ averageFps: 90 }) }),
      metrics: [{ metric: "AverageFps", buildAValue: 60, buildBValue: 90, delta: 30, deltaPercent: 50 }],
    });
    const user = userEvent.setup();

    renderWithQueryClient(<PerformancePage />);
    await screen.findByText("Compare Builds");

    const [buildASelect, buildBSelect] = screen.getAllByRole("combobox").slice(-2);
    await user.selectOptions(buildASelect, "build-1");
    await user.selectOptions(buildBSelect, "build-2");
    await user.click(screen.getByRole("button", { name: "Compare" }));

    expect(await screen.findByText("AverageFps")).toBeInTheDocument();
    expect(screen.getByText("+30.00 (+50.0%)")).toBeInTheDocument();
  });
});
