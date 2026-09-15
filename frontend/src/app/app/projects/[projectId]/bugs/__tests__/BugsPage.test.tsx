import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "./test-utils";
import type { Bug, BugListItem } from "@/lib/api/bugs";
import type { PagedResult } from "@/lib/api/pagination";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1" }),
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

vi.mock("@/lib/api/builds", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/builds")>("@/lib/api/builds");
  return {
    ...actual,
    buildsApi: { list: vi.fn(), create: vi.fn(), getById: vi.fn(), update: vi.fn(), archive: vi.fn(), restore: vi.fn() },
  };
});

import { bugsApi } from "@/lib/api/bugs";
import { buildsApi } from "@/lib/api/builds";
import BugsPage from "../page";

const mockedList = vi.mocked(bugsApi.list);
const mockedCreate = vi.mocked(bugsApi.create);
const mockedBuildsList = vi.mocked(buildsApi.list);

function makeBug(overrides: Partial<BugListItem> = {}): BugListItem {
  return {
    id: "bug-1",
    projectId: "project-1",
    title: "Weapon remains ADS after reload",
    severity: "HIGH",
    status: "OPEN",
    source: "WEB",
    build: null,
    reporter: { id: "user-1", displayName: "Victor" },
    createdAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function pagedResult(items: BugListItem[]): PagedResult<BugListItem> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

beforeEach(() => {
  vi.clearAllMocks();
  mockedBuildsList.mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 });
});

describe("BugsPage", () => {
  it("renders the bug list", async () => {
    mockedList.mockResolvedValue(pagedResult([makeBug()]));

    renderWithQueryClient(<BugsPage />);

    expect(await screen.findByText("Weapon remains ADS after reload")).toBeInTheDocument();
    expect(screen.getByText("Victor")).toBeInTheDocument();
  });

  it("shows the empty state when there are no bugs", async () => {
    mockedList.mockResolvedValue(pagedResult([]));

    renderWithQueryClient(<BugsPage />);

    expect(await screen.findByText("No bugs reported yet.")).toBeInTheDocument();
  });

  it("shows an error state when the list request fails", async () => {
    mockedList.mockRejectedValue(new Error("network error"));

    renderWithQueryClient(<BugsPage />);

    expect(await screen.findByText(/Failed to load bugs/i)).toBeInTheDocument();
  });

  it("validates the title before submitting the create bug form", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    const user = userEvent.setup();

    renderWithQueryClient(<BugsPage />);
    await user.click(await screen.findByRole("button", { name: "Create Bug" }));
    await user.click(screen.getByRole("button", { name: "Submit Bug" }));

    expect(await screen.findByText("Title is required.")).toBeInTheDocument();
    expect(mockedCreate).not.toHaveBeenCalled();
  });

  it("submits the create bug form and refreshes the list", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    mockedCreate.mockResolvedValue({} as Bug);
    const user = userEvent.setup();

    renderWithQueryClient(<BugsPage />);
    await user.click(await screen.findByRole("button", { name: "Create Bug" }));
    await user.type(screen.getByPlaceholderText("Weapon remains ADS after reload"), "Crash on load");
    await user.click(screen.getByRole("button", { name: "Submit Bug" }));

    await waitFor(() =>
      expect(mockedCreate).toHaveBeenCalledWith(
        "project-1",
        expect.objectContaining({ title: "Crash on load", source: "WEB" }),
      ),
    );
  });

  it("requests bugs filtered by the selected severity", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    const user = userEvent.setup();

    renderWithQueryClient(<BugsPage />);
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    const [severitySelect] = screen.getAllByRole("combobox");
    await user.selectOptions(severitySelect, "CRITICAL");

    await waitFor(() =>
      expect(mockedList).toHaveBeenLastCalledWith("project-1", expect.objectContaining({ severity: "CRITICAL" })),
    );
  });
});
