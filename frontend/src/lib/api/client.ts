const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export class ApiError extends Error {
  status: number;
  detail?: string;

  constructor(status: number, message: string, detail?: string) {
    super(message);
    this.status = status;
    this.detail = detail;
  }
}

export type TokenProvider = () => string | null;

let getAccessToken: TokenProvider = () => null;
let onUnauthorized: () => void = () => {};

export function configureApiClient(options: { getAccessToken: TokenProvider; onUnauthorized: () => void }) {
  getAccessToken = options.getAccessToken;
  onUnauthorized = options.onUnauthorized;
}

async function request<T>(path: string, options: RequestInit = {}, auth = true): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Content-Type", "application/json");

  if (auth) {
    const token = getAccessToken();
    if (token) headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${API_URL}${path}`, { ...options, headers });

  if (response.status === 401 && auth) {
    onUnauthorized();
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const isJson = response.headers.get("content-type")?.includes("application/json");
  const body = isJson ? await response.json().catch(() => undefined) : undefined;

  if (!response.ok) {
    throw new ApiError(response.status, body?.title ?? response.statusText, body?.detail);
  }

  return body as T;
}

export const apiClient = {
  get: <T>(path: string, auth = true) => request<T>(path, { method: "GET" }, auth),
  post: <T>(path: string, data?: unknown, auth = true) =>
    request<T>(path, { method: "POST", body: data ? JSON.stringify(data) : undefined }, auth),
  patch: <T>(path: string, data?: unknown, auth = true) =>
    request<T>(path, { method: "PATCH", body: data ? JSON.stringify(data) : undefined }, auth),
  delete: <T>(path: string, auth = true) => request<T>(path, { method: "DELETE" }, auth),
};
