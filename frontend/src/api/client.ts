import type { AuthResponse } from '../types';

export class ApiError extends Error {
    status: number;
    constructor(message: string, status: number) {
        super(message);
        this.status = status;
    }
}

// The access token lives only in memory (never localStorage). The refresh token is an HttpOnly cookie.
let accessToken: string | null = null;
let refreshing: Promise<AuthResponse | null> | null = null;

export function setAccessToken(token: string | null) {
    accessToken = token;
}

async function toApiError(res: Response): Promise<ApiError> {
    let message = `Request failed (${res.status}).`;
    try {
        const data = await res.json();
        if (data?.errors && typeof data.errors === 'object') {
            const first = (Object.values(data.errors) as string[][]).flat()[0];
            if (first) message = String(first);
        } else if (data?.title) {
            message = String(data.title);
        }
    } catch {
        /* body was empty or not JSON */
    }
    if (res.status === 429) message = 'Too many requests. Please wait a minute and try again.';
    return new ApiError(message, res.status);
}

/** Asks the backend for a new access token using the refresh cookie. Concurrent calls share one request. */
export function refreshSession(): Promise<AuthResponse | null> {
    if (!refreshing) {
        refreshing = (async () => {
            try {
                const res = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' });
                if (!res.ok) {
                    setAccessToken(null);
                    return null;
                }
                const data = (await res.json()) as AuthResponse;
                setAccessToken(data.accessToken);
                return data;
            } catch {
                return null;
            } finally {
                refreshing = null;
            }
        })();
    }
    return refreshing;
}

export async function api<T = void>(path: string, init: RequestInit = {}, canRetry = true): Promise<T> {
    const headers = new Headers(init.headers);
    if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
    if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);

    const res = await fetch(path, { ...init, headers, credentials: 'include' });

    // Access token expired: refresh once, then repeat the request.
    if (res.status === 401 && canRetry && accessToken !== null && !path.endsWith('/auth/refresh')) {
        const session = await refreshSession();
        if (session) return api<T>(path, init, false);
    }

    if (!res.ok) throw await toApiError(res);
    if (res.status === 204) return undefined as T;

    const text = await res.text();
    return (text ? JSON.parse(text) : undefined) as T;
}

export const post = <T = void>(path: string, body?: unknown) =>
    api<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) });

export const put = <T = void>(path: string, body: unknown) =>
    api<T>(path, { method: 'PUT', body: JSON.stringify(body) });
export const getAccessToken = () => accessToken;
export async function apiBlob(path: string, canRetry = true): Promise<{ blob: Blob; fileName: string }> {
  const headers = new Headers();
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);
  const res = await fetch(path, { headers, credentials: 'include' });

  if (res.status === 401 && canRetry && accessToken !== null) {
    const session = await refreshSession();
    if (session) return apiBlob(path, false);
  }
  if (!res.ok) throw await toApiError(res);

  const disposition = res.headers.get('Content-Disposition') ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  return { blob: await res.blob(), fileName: match ? decodeURIComponent(match[1]) : 'report.pdf' };
}
