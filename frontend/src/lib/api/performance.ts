import { apiClient } from "./client";
import type { PagedResult } from "./pagination";
import type { BuildConfigurationValue, BuildPlatform } from "./builds";

export interface PerformanceBuildSummary {
  id: string;
  version: string;
  buildNumber: string;
  platform: BuildPlatform;
  configuration: BuildConfigurationValue;
}

export interface PerformanceSummary {
  sampleCount: number;
  averageFps: number | null;
  minimumFps: number | null;
  averageFrameTimeMs: number | null;
  p50FrameTimeMs: number | null;
  p95FrameTimeMs: number | null;
  p99FrameTimeMs: number | null;
  maximumFrameTimeMs: number | null;
  averageMemoryUsedBytes: number | null;
  peakMemoryUsedBytes: number | null;
  averageGameThreadTimeMs: number | null;
  averageRenderThreadTimeMs: number | null;
  averageGpuTimeMs: number | null;
}

export interface PerformanceSessionListItem {
  sessionId: string;
  runtimeSessionId: string;
  build: PerformanceBuildSummary;
  startedAt: string;
  endedAt: string | null;
  platform: string | null;
  summary: PerformanceSummary;
}

export interface PerformanceSessionSummary {
  sessionId: string;
  runtimeSessionId: string;
  build: PerformanceBuildSummary;
  summary: PerformanceSummary;
}

export interface PerformanceSample {
  id: string;
  sequenceNumber: number;
  clientTimestamp: string;
  receivedAt: string;
  mapName: string | null;
  fps: number;
  frameTimeMs: number;
  gameThreadTimeMs: number | null;
  renderThreadTimeMs: number | null;
  gpuTimeMs: number | null;
  memoryUsedBytes: number | null;
  memoryAvailableBytes: number | null;
  cpuUtilizationPercent: number | null;
  gpuUtilizationPercent: number | null;
  drawCalls: number | null;
  playerCount: number | null;
  pingMs: number | null;
}

export interface PerformanceSeriesPoint {
  timestamp: string;
  averageFrameTimeMs: number | null;
  maximumFrameTimeMs: number | null;
  averageFps: number | null;
  minimumFps: number | null;
  averageMemoryUsedBytes: number | null;
}

export interface PerformanceSeries {
  points: PerformanceSeriesPoint[];
}

export interface MapBreakdownItem {
  mapName: string;
  sampleCount: number;
  averageFps: number | null;
  p95FrameTimeMs: number | null;
}

export interface BuildPerformanceListItem {
  buildId: string;
  version: string;
  buildNumber: string;
  platform: BuildPlatform;
  configuration: BuildConfigurationValue;
  isArchived: boolean;
  sessionCount: number;
  summary: PerformanceSummary;
}

export interface PerformanceCompareMetric {
  metric: string;
  buildAValue: number | null;
  buildBValue: number | null;
  delta: number | null;
  deltaPercent: number | null;
}

export interface PerformanceCompareResult {
  buildA: BuildPerformanceListItem;
  buildB: BuildPerformanceListItem;
  metrics: PerformanceCompareMetric[];
}

export interface ListPerformanceSessionsParams {
  page?: number;
  pageSize?: number;
  buildId?: string;
  from?: string;
  to?: string;
  platform?: string;
}

function buildSessionQueryString(params: ListPerformanceSessionsParams): string {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.buildId) query.set("buildId", params.buildId);
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);
  if (params.platform) query.set("platform", params.platform);
  const qs = query.toString();
  return qs ? `?${qs}` : "";
}

export const performanceApi = {
  list: (projectId: string, params: ListPerformanceSessionsParams = {}) =>
    apiClient.get<PagedResult<PerformanceSessionListItem>>(`/api/projects/${projectId}/performance/sessions${buildSessionQueryString(params)}`),

  getSessionSummary: (projectId: string, sessionId: string) =>
    apiClient.get<PerformanceSessionSummary>(`/api/projects/${projectId}/performance/sessions/${sessionId}`),

  getSamples: (projectId: string, sessionId: string, page = 1, pageSize = 100) =>
    apiClient.get<PagedResult<PerformanceSample>>(`/api/projects/${projectId}/performance/sessions/${sessionId}/samples?page=${page}&pageSize=${pageSize}`),

  getSeries: (projectId: string, sessionId: string, maxPoints = 500) =>
    apiClient.get<PerformanceSeries>(`/api/projects/${projectId}/performance/sessions/${sessionId}/series?maxPoints=${maxPoints}`),

  getMapBreakdown: (projectId: string, sessionId: string) =>
    apiClient.get<MapBreakdownItem[]>(`/api/projects/${projectId}/performance/sessions/${sessionId}/maps`),

  getBuilds: (projectId: string) => apiClient.get<BuildPerformanceListItem[]>(`/api/projects/${projectId}/performance/builds`),

  compare: (projectId: string, buildA: string, buildB: string) =>
    apiClient.get<PerformanceCompareResult>(`/api/projects/${projectId}/performance/compare?buildA=${buildA}&buildB=${buildB}`),
};
