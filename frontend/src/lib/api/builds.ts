import { apiClient } from "./client";

export type BuildPlatform = "WINDOWS" | "LINUX" | "MACOS";
export type BuildConfigurationValue = "DEBUG" | "DEBUG_GAME" | "DEVELOPMENT" | "TEST" | "SHIPPING";

export const BUILD_PLATFORMS: BuildPlatform[] = ["WINDOWS", "LINUX", "MACOS"];
export const BUILD_CONFIGURATIONS: BuildConfigurationValue[] = [
  "DEBUG",
  "DEBUG_GAME",
  "DEVELOPMENT",
  "TEST",
  "SHIPPING",
];

export const PLATFORM_LABELS: Record<BuildPlatform, string> = {
  WINDOWS: "Windows",
  LINUX: "Linux",
  MACOS: "macOS",
};

export const CONFIGURATION_LABELS: Record<BuildConfigurationValue, string> = {
  DEBUG: "Debug",
  DEBUG_GAME: "DebugGame",
  DEVELOPMENT: "Development",
  TEST: "Test",
  SHIPPING: "Shipping",
};

export interface Build {
  id: string;
  projectId: string;
  name: string | null;
  version: string;
  buildNumber: string;
  platform: BuildPlatform;
  configuration: BuildConfigurationValue;
  source: {
    branch: string | null;
    commitSha: string | null;
  };
  engineVersion: string | null;
  changelog: string | null;
  archivedAt: string | null;
  createdBy: {
    id: string;
    name: string;
  };
  createdAt: string;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CreateBuildInput {
  name?: string;
  version: string;
  buildNumber: string;
  platform: BuildPlatform;
  configuration: BuildConfigurationValue;
  branch?: string;
  commitSha?: string;
  engineVersion?: string;
  changelog?: string;
}

export interface UpdateBuildInput {
  version: string;
  name?: string;
  branch?: string;
  commitSha?: string;
  engineVersion?: string;
  changelog?: string;
}

export type BuildStatusFilter = "active" | "archived" | "all";

export interface ListBuildsParams {
  page?: number;
  pageSize?: number;
  platform?: BuildPlatform;
  configuration?: BuildConfigurationValue;
  status?: BuildStatusFilter;
  search?: string;
}

function buildQueryString(params: ListBuildsParams): string {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.platform) query.set("platform", params.platform);
  if (params.configuration) query.set("configuration", params.configuration);
  if (params.status) query.set("status", params.status);
  if (params.search) query.set("search", params.search);
  const qs = query.toString();
  return qs ? `?${qs}` : "";
}

export const buildsApi = {
  create: (projectId: string, input: CreateBuildInput) =>
    apiClient.post<Build>(`/api/projects/${projectId}/builds`, input),

  list: (projectId: string, params: ListBuildsParams = {}) =>
    apiClient.get<PagedResult<Build>>(`/api/projects/${projectId}/builds${buildQueryString(params)}`),

  getById: (projectId: string, buildId: string) =>
    apiClient.get<Build>(`/api/projects/${projectId}/builds/${buildId}`),

  update: (projectId: string, buildId: string, input: UpdateBuildInput) =>
    apiClient.patch<Build>(`/api/projects/${projectId}/builds/${buildId}`, input),

  archive: (projectId: string, buildId: string) =>
    apiClient.post<Build>(`/api/projects/${projectId}/builds/${buildId}/archive`),

  restore: (projectId: string, buildId: string) =>
    apiClient.post<Build>(`/api/projects/${projectId}/builds/${buildId}/restore`),
};
