import { apiClient } from "./client";

export interface Organization {
  id: string;
  name: string;
  slug: string;
  role: string;
  createdAt: string;
  updatedAt: string;
}

export const organizationsApi = {
  list: () => apiClient.get<Organization[]>("/api/organizations"),
  getById: (organizationId: string) => apiClient.get<Organization>(`/api/organizations/${organizationId}`),
};
