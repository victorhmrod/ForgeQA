import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "./test-utils";
import type { TelemetrySessionListItem } from "@/lib/api/telemetry";
import type { PagedResult } from "@/lib/api/pagination";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1" }),
}));

vi.mock("@/lib/api/telemetry", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/telemetry")>("@/lib/api/telemetry");
  return {
    ...actual,
    telemetryApi: { list: vi.fn(), getById: vi.fn(), getEvents: vi.fn() },
  };
});

vi.mock("@/lib/api/builds", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/builds")>("@/lib/api/builds");
  return {
    ...actual,
    buildsApi: { list: vi.fn(), create: vi.fn(), getById: vi.fn(), update: vi.fn(), archive: vi.fn(), restore: vi.fn() },
  };
});

import { telemetryApi } from "@/lib/api/telemetry";
import { buildsApi } from "@/lib/api/builds";
import TelemetryPage from "../page";

const mockedList = vi.mocked(telemetryApi.list);
const mockedBuildsList = vi.mocked(buildsApi.list);

function makeSession(overrides: Partial<TelemetrySessionListItem> = {}): TelemetrySessionListItem {
  return {
    id: "session-1",
    runtimeSessionId: "9818a0c3-20ea-45b2-ac73-1e89a1e2670e",
    build: { id: "build-1", version: "0.5.0", buildNumber: "42", platform: "WINDOWS", configuration: "DEVELOPMENT" },
    startedAt: "2026-01-01T00:00:00Z",
    endedAt: null,
    lastEventAt: "2026-01-01T00:05:00Z",
    eventCount: 3,
    platform: "WINDOWS",
    mapName: "Strike_Factory",
    ...overrides,
  };
}

function pagedResult(items: TelemetrySessionListItem[]): PagedResult<TelemetrySessionListItem> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

beforeEach(() => {
  vi.clearAllMocks();
  mockedBuildsList.mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 });
});

describe("TelemetryPage", () => {
  it("renders the session list", async () => {
    mockedList.mockResolvedValue(pagedResult([makeSession()]));

    renderWithQueryClient(<TelemetryPage />);

    expect(await screen.findByText("9818a0c3")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("shows the empty state when there are no sessions", async () => {
    mockedList.mockResolvedValue(pagedResult([]));

    renderWithQueryClient(<TelemetryPage />);

    expect(await screen.findByText("No telemetry sessions yet.")).toBeInTheDocument();
  });

  it("shows an error state when the list request fails", async () => {
    mockedList.mockRejectedValue(new Error("network error"));

    renderWithQueryClient(<TelemetryPage />);

    expect(await screen.findByText(/Failed to load telemetry sessions/i)).toBeInTheDocument();
  });

  it("shows Ended for a session with an endedAt", async () => {
    mockedList.mockResolvedValue(pagedResult([makeSession({ endedAt: "2026-01-01T00:10:00Z" })]));

    renderWithQueryClient(<TelemetryPage />);

    expect(await screen.findByText("Ended")).toBeInTheDocument();
  });

  it("requests sessions filtered by activeOnly", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    const user = userEvent.setup();

    renderWithQueryClient(<TelemetryPage />);
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    const [, activeSelect] = screen.getAllByRole("combobox");
    await user.selectOptions(activeSelect, "true");

    await waitFor(() =>
      expect(mockedList).toHaveBeenLastCalledWith("project-1", expect.objectContaining({ activeOnly: true })),
    );
  });
});
