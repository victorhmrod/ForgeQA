import { apiClient } from "./client";
import type { PagedResult } from "./pagination";
import type { BuildConfigurationValue, BuildPlatform } from "./builds";

export interface TelemetryBuildSummary {
  id: string;
  version: string;
  buildNumber: string;
  platform: BuildPlatform;
  configuration: BuildConfigurationValue;
}

export interface TelemetrySessionEnvironment {
  mapName: string | null;
  gameMode: string | null;
  platform: string | null;
  configuration: string | null;
  engineVersion: string | null;
  osVersion: string | null;
  locale: string | null;
}

export interface TelemetrySessionListItem {
  id: string;
  runtimeSessionId: string;
  build: TelemetryBuildSummary;
  startedAt: string;
  endedAt: string | null;
  lastEventAt: string | null;
  eventCount: number;
  platform: string | null;
  mapName: string | null;
}

export interface TelemetrySession {
  id: string;
  runtimeSessionId: string;
  build: TelemetryBuildSummary;
  startedAt: string;
  endedAt: string | null;
  lastEventAt: string | null;
  eventCount: number;
  environment: TelemetrySessionEnvironment;
}

export interface TelemetryEvent {
  id: string;
  sequenceNumber: number;
  eventName: string;
  clientTimestamp: string;
  receivedAt: string;
  category: string | null;
  properties: Record<string, unknown>;
  mapName: string | null;
}

export interface ListTelemetrySessionsParams {
  page?: number;
  pageSize?: number;
  buildId?: string;
  from?: string;
  to?: string;
  runtimeSessionId?: string;
  activeOnly?: boolean;
}

export interface ListTelemetryEventsParams {
  page?: number;
  pageSize?: number;
  eventName?: string;
}

function buildSessionQueryString(params: ListTelemetrySessionsParams): string {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.buildId) query.set("buildId", params.buildId);
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);
  if (params.runtimeSessionId) query.set("runtimeSessionId", params.runtimeSessionId);
  if (params.activeOnly !== undefined) query.set("activeOnly", String(params.activeOnly));
  const qs = query.toString();
  return qs ? `?${qs}` : "";
}

function buildEventsQueryString(params: ListTelemetryEventsParams): string {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.eventName) query.set("eventName", params.eventName);
  const qs = query.toString();
  return qs ? `?${qs}` : "";
}

export const telemetryApi = {
  list: (projectId: string, params: ListTelemetrySessionsParams = {}) =>
    apiClient.get<PagedResult<TelemetrySessionListItem>>(`/api/projects/${projectId}/telemetry/sessions${buildSessionQueryString(params)}`),

  getById: (projectId: string, sessionId: string) =>
    apiClient.get<TelemetrySession>(`/api/projects/${projectId}/telemetry/sessions/${sessionId}`),

  getEvents: (projectId: string, sessionId: string, params: ListTelemetryEventsParams = {}) =>
    apiClient.get<PagedResult<TelemetryEvent>>(`/api/projects/${projectId}/telemetry/sessions/${sessionId}/events${buildEventsQueryString(params)}`),
};
