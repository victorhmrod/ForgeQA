"use client";

import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { authApi, type CurrentUser } from "@/lib/api/auth";
import { configureApiClient, ApiError } from "@/lib/api/client";
import { tokenStorage } from "./token-storage";

interface AuthContextValue {
  user: CurrentUser | null;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, displayName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

configureApiClient({
  getAccessToken: () => tokenStorage.getAccessToken(),
  onUnauthorized: () => {
    tokenStorage.clear();
  },
});

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [isLoading, setIsLoading] = useState(() => !!tokenStorage.getAccessToken());

  useEffect(() => {
    if (!tokenStorage.getAccessToken()) return;

    authApi
      .me()
      .then(setUser)
      .catch(() => tokenStorage.clear())
      .finally(() => setIsLoading(false));
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const response = await authApi.login({ email, password });
    tokenStorage.setTokens(response.accessToken, response.refreshToken);
    setUser(response.user);
  }, []);

  const register = useCallback(async (email: string, displayName: string, password: string) => {
    const response = await authApi.register({ email, displayName, password });
    tokenStorage.setTokens(response.accessToken, response.refreshToken);
    setUser(response.user);
  }, []);

  const logout = useCallback(async () => {
    const refreshToken = tokenStorage.getRefreshToken();
    tokenStorage.clear();
    setUser(null);
    if (refreshToken) {
      await authApi.logout(refreshToken).catch(() => undefined);
    }
  }, []);

  return (
    <AuthContext.Provider value={{ user, isLoading, login, register, logout }}>{children}</AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within an AuthProvider");
  return context;
}

export { ApiError };
