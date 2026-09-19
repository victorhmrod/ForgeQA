import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "./test-utils";
import type { ProjectApiKey, ProjectApiKeyCreated } from "@/lib/api/api-keys";

vi.mock("next/navigation", () => ({
  useParams: () => ({ projectId: "project-1" }),
}));

vi.mock("@/lib/api/api-keys", () => ({
  apiKeysApi: {
    list: vi.fn(),
    create: vi.fn(),
    revoke: vi.fn(),
  },
  PROJECT_API_KEY_SCOPES: ["BUG_REPORT_WRITE", "TELEMETRY_WRITE", "PERFORMANCE_WRITE"],
  PROJECT_API_KEY_SCOPE_LABELS: {
    BUG_REPORT_WRITE: "Bug Report Write",
    TELEMETRY_WRITE: "Telemetry Write",
    PERFORMANCE_WRITE: "Performance Write",
  },
}));

import { apiKeysApi } from "@/lib/api/api-keys";
import ApiKeysPage from "../page";

const mockedList = vi.mocked(apiKeysApi.list);
const mockedCreate = vi.mocked(apiKeysApi.create);
const mockedRevoke = vi.mocked(apiKeysApi.revoke);

function makeKey(overrides: Partial<ProjectApiKey> = {}): ProjectApiKey {
  return {
    id: "key-1",
    name: "Unreal Runtime",
    prefix: "ab12cd34",
    scopes: ["BUG_REPORT_WRITE"],
    createdAt: "2026-01-01T00:00:00Z",
    lastUsedAt: null,
    revokedAt: null,
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("ApiKeysPage", () => {
  it("renders the key list", async () => {
    mockedList.mockResolvedValue([makeKey()]);

    renderWithQueryClient(<ApiKeysPage />);

    expect(await screen.findByText("Unreal Runtime")).toBeInTheDocument();
    // "Bug Report Write" also appears as a scope checkbox label in the create form.
    expect(screen.getAllByText("Bug Report Write").length).toBeGreaterThan(0);
  });

  it("shows both scopes for a key holding multiple", async () => {
    mockedList.mockResolvedValue([makeKey({ scopes: ["BUG_REPORT_WRITE", "TELEMETRY_WRITE"] })]);

    renderWithQueryClient(<ApiKeysPage />);

    expect(await screen.findByText("Bug Report Write, Telemetry Write")).toBeInTheDocument();
  });

  it("shows the empty state when there are no keys", async () => {
    mockedList.mockResolvedValue([]);

    renderWithQueryClient(<ApiKeysPage />);

    expect(await screen.findByText(/No API keys yet/)).toBeInTheDocument();
  });

  it("shows the plaintext secret exactly once after creation", async () => {
    mockedList.mockResolvedValue([]);
    const created: ProjectApiKeyCreated = { ...makeKey(), plaintextKey: "fqa_proj_ab12cd34_secretsecret" };
    mockedCreate.mockResolvedValue(created);
    const user = userEvent.setup();

    renderWithQueryClient(<ApiKeysPage />);
    await user.type(await screen.findByPlaceholderText("Unreal Runtime"), "Unreal Runtime");
    await user.click(screen.getByRole("button", { name: "Create key" }));

    expect(await screen.findByText("fqa_proj_ab12cd34_secretsecret")).toBeInTheDocument();
    expect(screen.getByText(/cannot be shown again/i)).toBeInTheDocument();
    expect(mockedCreate).toHaveBeenCalledWith("project-1", "Unreal Runtime", ["BUG_REPORT_WRITE"]);
  });

  it("lets the user select the Telemetry Write scope before creating a key", async () => {
    mockedList.mockResolvedValue([]);
    mockedCreate.mockResolvedValue({ ...makeKey(), plaintextKey: "fqa_proj_ab12cd34_secretsecret" });
    const user = userEvent.setup();

    renderWithQueryClient(<ApiKeysPage />);
    await user.type(await screen.findByPlaceholderText("Unreal Runtime"), "Game Server");
    await user.click(screen.getByLabelText("Telemetry Write"));
    await user.click(screen.getByRole("button", { name: "Create key" }));

    await waitFor(() =>
      expect(mockedCreate).toHaveBeenCalledWith("project-1", "Game Server", ["BUG_REPORT_WRITE", "TELEMETRY_WRITE"]),
    );
  });

  it("lets the user select the Performance Write scope before creating a key", async () => {
    mockedList.mockResolvedValue([]);
    mockedCreate.mockResolvedValue({ ...makeKey(), plaintextKey: "fqa_proj_ab12cd34_secretsecret" });
    const user = userEvent.setup();

    renderWithQueryClient(<ApiKeysPage />);
    await user.type(await screen.findByPlaceholderText("Unreal Runtime"), "Perf Runtime");
    await user.click(screen.getByLabelText("Performance Write"));
    await user.click(screen.getByRole("button", { name: "Create key" }));

    await waitFor(() =>
      expect(mockedCreate).toHaveBeenCalledWith("project-1", "Perf Runtime", ["BUG_REPORT_WRITE", "PERFORMANCE_WRITE"]),
    );
  });

  it("revokes a key", async () => {
    mockedList.mockResolvedValue([makeKey()]);
    mockedRevoke.mockResolvedValue(makeKey({ revokedAt: "2026-02-01T00:00:00Z" }));
    const user = userEvent.setup();

    renderWithQueryClient(<ApiKeysPage />);
    await user.click(await screen.findByRole("button", { name: "Revoke" }));

    await waitFor(() => expect(mockedRevoke).toHaveBeenCalledWith("project-1", "key-1"));
  });

  it("shows an error state when the list request fails", async () => {
    mockedList.mockRejectedValue(new Error("network error"));

    renderWithQueryClient(<ApiKeysPage />);

    expect(await screen.findByText(/Failed to load API keys/i)).toBeInTheDocument();
  });
});
