import "server-only";
import { cookies } from "next/headers";

/**
 * Server-only session handling for the backend-for-frontend.
 *
 * Tokens issued by the FarmHand API never reach browser JavaScript: the access and refresh tokens
 * live in HttpOnly cookies set by Next.js route handlers, and every API call goes through the
 * same-origin `/api/bff/*` proxy, which attaches the bearer token server-side.
 */

export const ACCESS_COOKIE = "fh_access";
export const REFRESH_COOKIE = "fh_refresh";

const REFRESH_MAX_AGE = 60 * 60 * 24 * 7;

export function apiBaseUrl(): string {
  const url = process.env.FARMHAND_API_URL ?? "http://127.0.0.1:5099";
  return url.replace(/\/+$/, "");
}

function secureCookies(): boolean {
  if (process.env.SESSION_COOKIE_SECURE) return process.env.SESSION_COOKIE_SECURE === "true";
  return process.env.NODE_ENV === "production";
}

export type TokenPair = {
  token: string;
  refreshToken: string;
  expiresAt: string;
};

export type LoginPayload = TokenPair & {
  userId: string;
  email: string;
  role: string;
  fullName: string | null;
  mustChangePassword: boolean;
};

type CookieWriter = {
  set: (name: string, value: string, options: Record<string, unknown>) => unknown;
};

export function writeSessionCookies(target: CookieWriter, tokens: TokenPair) {
  const accessMaxAge = Math.max(60, Math.floor((Date.parse(tokens.expiresAt) - Date.now()) / 1000));
  const base = { httpOnly: true, secure: secureCookies(), sameSite: "lax" as const, path: "/" };
  target.set(ACCESS_COOKIE, tokens.token, { ...base, maxAge: accessMaxAge });
  target.set(REFRESH_COOKIE, tokens.refreshToken, { ...base, maxAge: REFRESH_MAX_AGE });
}

export function clearSessionCookies(target: CookieWriter) {
  const base = { httpOnly: true, secure: secureCookies(), sameSite: "lax" as const, path: "/", maxAge: 0 };
  target.set(ACCESS_COOKIE, "", base);
  target.set(REFRESH_COOKIE, "", base);
}

/** Decodes a JWT payload without verifying it (verification is the API's job). */
export function decodeJwt(token: string | undefined): Record<string, unknown> | null {
  if (!token) return null;
  const part = token.split(".")[1];
  if (!part) return null;
  try {
    return JSON.parse(Buffer.from(part.replace(/-/g, "+").replace(/_/g, "/"), "base64").toString("utf8")) as Record<string, unknown>;
  } catch {
    return null;
  }
}

export function tokenExpiresSoon(token: string | undefined, withinSeconds = 30): boolean {
  const exp = decodeJwt(token)?.exp;
  if (typeof exp !== "number") return true;
  return exp * 1000 - Date.now() < withinSeconds * 1000;
}

/** Forwards the caller's address so the API can rate-limit per client (it trusts this proxy only). */
export function forwardedFor(request: Request): Record<string, string> {
  const xff = request.headers.get("x-forwarded-for");
  const realIp = request.headers.get("x-real-ip");
  const value = xff ?? realIp;
  return value ? { "x-forwarded-for": value } : {};
}

// Single-flight refresh: concurrent requests that find an expired access token share one refresh
// call, so the API's refresh-token reuse detection is never triggered by our own parallel requests.
const inflight = new Map<string, { promise: Promise<TokenPair | null>; at: number }>();

export async function refreshTokens(refreshToken: string, request?: Request): Promise<TokenPair | null> {
  const now = Date.now();
  for (const [key, entry] of inflight) {
    if (now - entry.at > 20_000) inflight.delete(key);
  }

  const existing = inflight.get(refreshToken);
  if (existing) return existing.promise;

  const promise = (async () => {
    try {
      const response = await fetch(`${apiBaseUrl()}/api/auth/refresh`, {
        method: "POST",
        headers: { "content-type": "application/json", ...(request ? forwardedFor(request) : {}) },
        body: JSON.stringify({ refreshToken }),
        cache: "no-store",
      });
      if (!response.ok) return null;
      const body = (await response.json()) as TokenPair;
      return { token: body.token, refreshToken: body.refreshToken, expiresAt: body.expiresAt };
    } catch {
      return null;
    }
  })();

  inflight.set(refreshToken, { promise, at: now });
  return promise;
}

/**
 * Returns a usable access token for the current request, refreshing it if needed.
 * `rotated` is set when new tokens must be written back to the browser.
 */
export async function resolveAccessToken(request?: Request): Promise<{ token: string | null; rotated: TokenPair | null; expired: boolean }> {
  const store = await cookies();
  const access = store.get(ACCESS_COOKIE)?.value;
  const refresh = store.get(REFRESH_COOKIE)?.value;

  if (access && !tokenExpiresSoon(access)) return { token: access, rotated: null, expired: false };
  if (!refresh) return { token: null, rotated: null, expired: Boolean(access) };

  const rotated = await refreshTokens(refresh, request);
  if (!rotated) return { token: null, rotated: null, expired: true };
  return { token: rotated.token, rotated, expired: false };
}

/** Same-origin check for state-changing requests (CSRF defence in depth on top of SameSite cookies). */
export function isTrustedMutation(request: Request): boolean {
  if (request.method === "GET" || request.method === "HEAD") return true;
  if (request.headers.get("x-farmhand-csrf") !== "1") return false;
  const origin = request.headers.get("origin");
  if (!origin) return true;
  const host = request.headers.get("x-forwarded-host") ?? request.headers.get("host");
  try {
    return new URL(origin).host === host;
  } catch {
    return false;
  }
}

export function problem(status: number, title: string, code?: string, detail?: string) {
  return Response.json(
    { status, title, ...(detail ? { detail } : {}), ...(code ? { code } : {}) },
    { status, headers: { "content-type": "application/problem+json", "cache-control": "no-store" } },
  );
}
