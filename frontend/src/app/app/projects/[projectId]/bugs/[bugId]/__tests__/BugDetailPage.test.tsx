import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithQueryClient } from "../../__tests__/test-utils";
import type { Bug } from "@/lib/api/bugs";
import type { TelemetrySessionListItem } from "@/lib/api/telemetry";
import type { PagedResult } from "@/lib/api/pagination";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1", bugId: "bug-1" }),
}));

vi.mock("@/lib/api/bugs", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/bugs")>("@/lib/api/bugs");
  return {
    ...actual,
    bugsApi: {
      list: vi.fn(),
      create: vi.fn(),
      getById: vi.fn(),
      update: vi.fn(),
      createAttachmentDownload: vi.fn(),
    },
  };
});

vi.mock("@/lib/api/telemetry", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/telemetry")>("@/lib/api/telemetry");
  return {
    ...actual,
    telemetryApi: { list: vi.fn(), getById: vi.fn(), getEvents: vi.fn() },
  };
});

import { bugsApi } from "@/lib/api/bugs";
import { telemetryApi } from "@/lib/api/telemetry";
import BugDetailPage from "../page";

const mockedGetById = vi.mocked(bugsApi.getById);
const mockedTelemetryList = vi.mocked(telemetryApi.list);

function makeBug(overrides: Partial<Bug> = {}): Bug {
  return {
    id: "bug-1",
    projectId: "project-1",
    build: null,
    title: "Crash on map load",
    description: "Crashes on load.",
    reproductionSteps: null,
    severity: "CRITICAL",
    status: "OPEN",
    source: "UNREAL_RUNTIME",
    runtimeSessionId: null,
    environment: {
      mapName: null, gameMode: null, platform: null, engineVersion: null,
      osVersion: null, cpu: null, gpu: null, memoryBytes: null, locale: null,
    },
    reporter: { id: null, displayName: null },
    attachments: [],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    resolvedAt: null,
    closedAt: null,
    ...overrides,
  };
}

function pagedSessions(items: TelemetrySessionListItem[]): PagedResult<TelemetrySessionListItem> {
  return { items, page: 1, pageSize: 1, totalCount: items.length, totalPages: items.length > 0 ? 1 : 0 };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("BugDetailPage telemetry correlation", () => {
  it("shows a telemetry session link when a matching session exists", async () => {
    mockedGetById.mockResolvedValue(makeBug({ runtimeSessionId: "9818a0c3-20ea-45b2-ac73-1e89a1e2670e" }));
    mockedTelemetryList.mockResolvedValue(
      pagedSessions([
        {
          id: "session-1",
          runtimeSessionId: "9818a0c3-20ea-45b2-ac73-1e89a1e2670e",
          build: { id: "build-1", version: "0.5.0", buildNumber: "42", platform: "WINDOWS", configuration: "DEVELOPMENT" },
          startedAt: "2026-01-01T00:00:00Z",
          endedAt: null,
          lastEventAt: null,
          eventCount: 0,
          platform: "WINDOWS",
          mapName: null,
        },
      ]),
    );

    renderWithQueryClient(<BugDetailPage />);

    const link = await screen.findByRole("link", { name: /View telemetry session/ });
    expect(link).toHaveAttribute("href", "/app/projects/project-1/telemetry/session-1");
  });

  it("does not show a telemetry link when no runtimeSessionId is present", async () => {
    mockedGetById.mockResolvedValue(makeBug({ runtimeSessionId: null }));

    renderWithQueryClient(<BugDetailPage />);

    await screen.findByText("Crash on map load");
    expect(screen.queryByRole("link", { name: /View telemetry session/ })).not.toBeInTheDocument();
    expect(mockedTelemetryList).not.toHaveBeenCalled();
  });

  it("does not show a telemetry link when no matching session is found", async () => {
    mockedGetById.mockResolvedValue(makeBug({ runtimeSessionId: "9818a0c3-20ea-45b2-ac73-1e89a1e2670e" }));
    mockedTelemetryList.mockResolvedValue(pagedSessions([]));

    renderWithQueryClient(<BugDetailPage />);

    await screen.findByText("9818a0c3-20ea-45b2-ac73-1e89a1e2670e");
    expect(screen.queryByRole("link", { name: /View telemetry session/ })).not.toBeInTheDocument();
  });
});
