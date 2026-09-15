import { apiClient } from "./client";

export interface Project {
  id: string;
  organizationId: string;
  name: string;
  slug: string;
  description: string | null;
  createdAt: string;
  updatedAt: string;
}

export const projectsApi = {
  create: (organizationId: string, input: { name: string; description?: string }) =>
    apiClient.post<Project>(`/api/organizations/${organizationId}/projects`, input),

  listForOrganization: (organizationId: string) =>
    apiClient.get<Project[]>(`/api/organizations/${organizationId}/projects`),

  getById: (projectId: string) => apiClient.get<Project>(`/api/projects/${projectId}`),
};
