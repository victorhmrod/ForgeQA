import { apiClient } from "./client";

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: CurrentUser;
}

export const authApi = {
  register: (input: { email: string; displayName: string; password: string }) =>
    apiClient.post<AuthResponse>("/api/auth/register", input, false),

  login: (input: { email: string; password: string }) =>
    apiClient.post<AuthResponse>("/api/auth/login", input, false),

  refresh: (refreshToken: string) =>
    apiClient.post<AuthResponse>("/api/auth/refresh", { refreshToken }, false),

  logout: (refreshToken: string) => apiClient.post<void>("/api/auth/logout", { refreshToken }, false),

  me: () => apiClient.get<CurrentUser>("/api/auth/me"),
};
