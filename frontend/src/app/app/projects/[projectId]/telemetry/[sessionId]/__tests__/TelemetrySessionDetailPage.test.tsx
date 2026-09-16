import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "../../__tests__/test-utils";
import type { TelemetryEvent, TelemetrySession } from "@/lib/api/telemetry";
import type { PagedResult } from "@/lib/api/pagination";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1", sessionId: "session-1" }),
}));

vi.mock("@/lib/api/telemetry", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/telemetry")>("@/lib/api/telemetry");
  return {
    ...actual,
    telemetryApi: { list: vi.fn(), getById: vi.fn(), getEvents: vi.fn() },
  };
});

import { telemetryApi } from "@/lib/api/telemetry";
import TelemetrySessionDetailPage from "../page";

const mockedGetById = vi.mocked(telemetryApi.getById);
const mockedGetEvents = vi.mocked(telemetryApi.getEvents);

function makeSession(overrides: Partial<TelemetrySession> = {}): TelemetrySession {
  return {
    id: "session-1",
    runtimeSessionId: "9818a0c3-20ea-45b2-ac73-1e89a1e2670e",
    build: { id: "build-1", version: "0.5.0", buildNumber: "42", platform: "WINDOWS", configuration: "DEVELOPMENT" },
    startedAt: "2026-01-01T00:00:00Z",
    endedAt: null,
    lastEventAt: "2026-01-01T00:05:00Z",
    eventCount: 1,
    environment: {
      mapName: "Strike_Factory",
      gameMode: "Strike",
      platform: "WINDOWS",
      configuration: "DEVELOPMENT",
      engineVersion: "UE 5.8.2",
      osVersion: "Windows 11",
      locale: "en-US",
    },
    ...overrides,
  };
}

function makeEvent(overrides: Partial<TelemetryEvent> = {}): TelemetryEvent {
  return {
    id: "event-1",
    sequenceNumber: 1,
    eventName: "weapon.fired",
    clientTimestamp: "2026-01-01T00:01:00Z",
    receivedAt: "2026-01-01T00:01:01Z",
    category: null,
    properties: { weaponId: "rifle_a" },
    mapName: null,
    ...overrides,
  };
}

function pagedEvents(items: TelemetryEvent[]): PagedResult<TelemetryEvent> {
  return { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("TelemetrySessionDetailPage", () => {
  it("renders session, build, environment, and the event timeline", async () => {
    mockedGetById.mockResolvedValue(makeSession());
    mockedGetEvents.mockResolvedValue(pagedEvents([makeEvent()]));

    renderWithQueryClient(<TelemetrySessionDetailPage />);

    expect(await screen.findByText("9818a0c3-20ea-45b2-ac73-1e89a1e2670e")).toBeInTheDocument();
    expect(screen.getByText("Strike_Factory")).toBeInTheDocument();
    expect(screen.getByText("weapon.fired")).toBeInTheDocument();
    expect(screen.getByText(/"weaponId": "rifle_a"/)).toBeInTheDocument();
  });

  it("shows the empty state when there are no events", async () => {
    mockedGetById.mockResolvedValue(makeSession({ eventCount: 0 }));
    mockedGetEvents.mockResolvedValue(pagedEvents([]));

    renderWithQueryClient(<TelemetrySessionDetailPage />);

    expect(await screen.findByText("No events recorded yet.")).toBeInTheDocument();
  });

  it("shows an error state when the session is not found", async () => {
    mockedGetById.mockRejectedValue(new Error("not found"));

    renderWithQueryClient(<TelemetrySessionDetailPage />);

    expect(await screen.findByText("Telemetry session not found.")).toBeInTheDocument();
  });

  it("paginates events via Load more", async () => {
    mockedGetById.mockResolvedValue(makeSession({ eventCount: 150 }));
    mockedGetEvents.mockResolvedValue({ items: [makeEvent()], page: 1, pageSize: 100, totalCount: 150, totalPages: 2 });
    const user = userEvent.setup();

    renderWithQueryClient(<TelemetrySessionDetailPage />);
    await screen.findByText("weapon.fired");
    await user.click(screen.getByRole("button", { name: "Load more" }));

    await waitFor(() => expect(mockedGetEvents).toHaveBeenLastCalledWith("project-1", "session-1", { page: 2, pageSize: 100 }));
  });
});
