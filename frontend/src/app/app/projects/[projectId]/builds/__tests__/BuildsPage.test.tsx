import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "./test-utils";
import type { Build, PagedResult } from "@/lib/api/builds";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1" }),
}));

vi.mock("@/lib/api/builds", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api/builds")>("@/lib/api/builds");
  return {
    ...actual,
    buildsApi: {
      list: vi.fn(),
      create: vi.fn(),
      archive: vi.fn(),
      restore: vi.fn(),
      getById: vi.fn(),
      update: vi.fn(),
    },
  };
});

import { buildsApi } from "@/lib/api/builds";
import BuildsPage from "../page";

const mockedList = vi.mocked(buildsApi.list);
const mockedCreate = vi.mocked(buildsApi.create);
const mockedArchive = vi.mocked(buildsApi.archive);

function makeBuild(overrides: Partial<Build> = {}): Build {
  return {
    id: "build-1",
    projectId: "project-1",
    name: "QA Candidate",
    version: "0.4.2",
    buildNumber: "1842",
    platform: "WINDOWS",
    configuration: "DEVELOPMENT",
    source: { branch: "main", commitSha: "a941de3" },
    engineVersion: "UE 5.8.2",
    changelog: null,
    archivedAt: null,
    createdBy: { id: "user-1", name: "Victor" },
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function pagedResult(items: Build[]): PagedResult<Build> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("BuildsPage", () => {
  it("renders the build list", async () => {
    mockedList.mockResolvedValue(pagedResult([makeBuild()]));

    renderWithQueryClient(<BuildsPage />);

    expect(await screen.findByText("QA Candidate")).toBeInTheDocument();
    expect(screen.getByText("0.4.2")).toBeInTheDocument();
    expect(screen.getByText("1842")).toBeInTheDocument();
  });

  it("shows the empty state when there are no builds", async () => {
    mockedList.mockResolvedValue(pagedResult([]));

    renderWithQueryClient(<BuildsPage />);

    expect(await screen.findByText("No builds registered yet.")).toBeInTheDocument();
  });

  it("shows an error state when the list request fails", async () => {
    mockedList.mockRejectedValue(new Error("network error"));

    renderWithQueryClient(<BuildsPage />);

    expect(await screen.findByText(/Failed to load builds/i)).toBeInTheDocument();
  });

  it("validates required fields before submitting the register form", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    const user = userEvent.setup();

    renderWithQueryClient(<BuildsPage />);
    await user.click(await screen.findByRole("button", { name: "Register Build" }));
    await user.type(screen.getByPlaceholderText("0.4.2"), "1.0.0");
    await user.click(screen.getByRole("button", { name: "Register" }));

    expect(await screen.findByText("Build number is required.")).toBeInTheDocument();
    expect(mockedCreate).not.toHaveBeenCalled();
  });

  it("submits the register build form and refreshes the list", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    mockedCreate.mockResolvedValue(makeBuild());
    const user = userEvent.setup();

    renderWithQueryClient(<BuildsPage />);
    await user.click(await screen.findByRole("button", { name: "Register Build" }));

    await user.type(screen.getByPlaceholderText("0.4.2"), "1.0.0");
    await user.type(screen.getByPlaceholderText("1842"), "42");
    await user.click(screen.getByRole("button", { name: "Register" }));

    await waitFor(() => expect(mockedCreate).toHaveBeenCalledWith(
      "project-1",
      expect.objectContaining({ version: "1.0.0", buildNumber: "42" }),
    ));
  });

  it("requests builds filtered by the selected platform", async () => {
    mockedList.mockResolvedValue(pagedResult([]));
    const user = userEvent.setup();

    renderWithQueryClient(<BuildsPage />);
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    const [platformSelect] = screen.getAllByRole("combobox");
    await user.selectOptions(platformSelect, "LINUX");

    await waitFor(() =>
      expect(mockedList).toHaveBeenLastCalledWith(
        "project-1",
        expect.objectContaining({ platform: "LINUX" }),
      ),
    );
  });

  it("archives a build from the list", async () => {
    mockedList.mockResolvedValue(pagedResult([makeBuild()]));
    mockedArchive.mockResolvedValue(makeBuild({ archivedAt: "2026-02-01T00:00:00Z" }));
    const user = userEvent.setup();

    renderWithQueryClient(<BuildsPage />);
    const row = (await screen.findByText("QA Candidate")).closest("tr")!;
    await user.click(within(row).getByRole("button", { name: "Archive" }));

    await waitFor(() => expect(mockedArchive).toHaveBeenCalledWith("project-1", "build-1"));
  });
});
