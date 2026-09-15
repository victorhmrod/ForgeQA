import { apiClient } from "./client";
import type { PagedResult } from "./pagination";
import type { BuildConfigurationValue, BuildPlatform } from "./builds";

export type BugSeverity = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export type BugStatusValue = "OPEN" | "IN_PROGRESS" | "RESOLVED" | "CLOSED";
export type BugSource = "UNREAL_RUNTIME" | "WEB" | "API";
export type BugAttachmentType = "SCREENSHOT" | "LOG" | "OTHER";
export type BugAttachmentStatusValue = "PENDING" | "READY" | "FAILED";

export const BUG_SEVERITIES: BugSeverity[] = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];
export const BUG_STATUSES: BugStatusValue[] = ["OPEN", "IN_PROGRESS", "RESOLVED", "CLOSED"];

export const SEVERITY_LABELS: Record<BugSeverity, string> = {
  LOW: "Low",
  MEDIUM: "Medium",
  HIGH: "High",
  CRITICAL: "Critical",
};

export const STATUS_LABELS: Record<BugStatusValue, string> = {
  OPEN: "Open",
  IN_PROGRESS: "In Progress",
  RESOLVED: "Resolved",
  CLOSED: "Closed",
};

export const SOURCE_LABELS: Record<BugSource, string> = {
  UNREAL_RUNTIME: "Unreal Runtime",
  WEB: "Web",
  API: "API",
};

export interface BugBuildSummary {
  id: string;
  version: string;
  buildNumber: string;
  platform: BuildPlatform;
  configuration: BuildConfigurationValue;
}

export interface BugReporter {
  id: string | null;
  displayName: string | null;
}

export interface BugEnvironment {
  mapName: string | null;
  gameMode: string | null;
  platform: string | null;
  engineVersion: string | null;
  osVersion: string | null;
  cpu: string | null;
  gpu: string | null;
  memoryBytes: number | null;
  locale: string | null;
}

export interface BugAttachment {
  id: string;
  type: BugAttachmentType;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: BugAttachmentStatusValue;
  createdAt: string;
}

export interface BugListItem {
  id: string;
  projectId: string;
  title: string;
  severity: BugSeverity;
  status: BugStatusValue;
  source: BugSource;
  build: BugBuildSummary | null;
  reporter: BugReporter;
  createdAt: string;
}

export interface Bug {
  id: string;
  projectId: string;
  build: BugBuildSummary | null;
  title: string;
  description: string | null;
  reproductionSteps: string | null;
  severity: BugSeverity;
  status: BugStatusValue;
  source: BugSource;
  runtimeSessionId: string | null;
  environment: BugEnvironment;
  reporter: BugReporter;
  attachments: BugAttachment[];
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
}

export interface CreateBugInput {
  buildId?: string;
  title: string;
  description?: string;
  reproductionSteps?: string;
  severity: BugSeverity;
  source: BugSource;
}

export interface UpdateBugInput {
  title: string;
  description?: string;
  reproductionSteps?: string;
  severity: BugSeverity;
  status: BugStatusValue;
}

export interface ListBugsParams {
  page?: number;
  pageSize?: number;
  status?: BugStatusValue;
  severity?: BugSeverity;
  buildId?: string;
  source?: BugSource;
  search?: string;
}

function buildQueryString(params: ListBugsParams): string {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.status) query.set("status", params.status);
  if (params.severity) query.set("severity", params.severity);
  if (params.buildId) query.set("buildId", params.buildId);
  if (params.source) query.set("source", params.source);
  if (params.search) query.set("search", params.search);
  const qs = query.toString();
  return qs ? `?${qs}` : "";
}

export interface BugAttachmentDownload {
  url: string;
  expiresAt: string;
}

export const bugsApi = {
  create: (projectId: string, input: CreateBugInput) => apiClient.post<Bug>(`/api/projects/${projectId}/bugs`, input),

  list: (projectId: string, params: ListBugsParams = {}) =>
    apiClient.get<PagedResult<BugListItem>>(`/api/projects/${projectId}/bugs${buildQueryString(params)}`),

  getById: (projectId: string, bugId: string) => apiClient.get<Bug>(`/api/projects/${projectId}/bugs/${bugId}`),

  update: (projectId: string, bugId: string, input: UpdateBugInput) =>
    apiClient.patch<Bug>(`/api/projects/${projectId}/bugs/${bugId}`, input),

  createAttachmentDownload: (projectId: string, bugId: string, attachmentId: string) =>
    apiClient.post<BugAttachmentDownload>(`/api/projects/${projectId}/bugs/${bugId}/attachments/${attachmentId}/download`),
};
