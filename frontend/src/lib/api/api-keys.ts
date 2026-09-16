import { apiClient } from "./client";

export type ProjectApiKeyScope = "BUG_REPORT_WRITE" | "TELEMETRY_WRITE";

export const PROJECT_API_KEY_SCOPES: ProjectApiKeyScope[] = ["BUG_REPORT_WRITE", "TELEMETRY_WRITE"];

export const PROJECT_API_KEY_SCOPE_LABELS: Record<ProjectApiKeyScope, string> = {
  BUG_REPORT_WRITE: "Bug Report Write",
  TELEMETRY_WRITE: "Telemetry Write",
};

export interface ProjectApiKey {
  id: string;
  name: string;
  prefix: string;
  scopes: ProjectApiKeyScope[];
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface ProjectApiKeyCreated extends ProjectApiKey {
  plaintextKey: string;
}

export const apiKeysApi = {
  create: (projectId: string, name: string, scopes: ProjectApiKeyScope[]) =>
    apiClient.post<ProjectApiKeyCreated>(`/api/projects/${projectId}/api-keys`, { name, scopes }),

  list: (projectId: string) => apiClient.get<ProjectApiKey[]>(`/api/projects/${projectId}/api-keys`),

  revoke: (projectId: string, keyId: string) =>
    apiClient.post<ProjectApiKey>(`/api/projects/${projectId}/api-keys/${keyId}/revoke`),
};
