import { NextResponse } from "next/server";
import {
  apiBaseUrl,
  clearSessionCookies,
  forwardedFor,
  isTrustedMutation,
  problem,
  refreshTokens,
  resolveAccessToken,
  writeSessionCookies,
  REFRESH_COOKIE,
  type TokenPair,
} from "@/lib/server/session";
import { cookies } from "next/headers";
import { BodyTooLargeError, readBoundedBody } from "@/lib/server/request-body";

export const dynamic = "force-dynamic";

/** Token-issuing routes must go through /api/session so tokens never reach browser JavaScript. */
const BLOCKED = new Set(["auth/login", "auth/register", "auth/refresh", "auth/logout"]);
const FORWARD_REQUEST_HEADERS = ["content-type", "accept", "idempotency-key"];
const FORWARD_RESPONSE_HEADERS = ["content-type", "retry-after", "content-disposition", "cache-control", "location"];
const MAX_BODY_BYTES = 25 * 1024 * 1024;

async function handle(request: Request, ctx: { params: Promise<{ path: string[] }> }) {
  const { path } = await ctx.params;
  const joined = path.map(encodeURIComponent).join("/");

  if (BLOCKED.has(joined.toLowerCase())) return problem(404, "Not found.");
  if (!isTrustedMutation(request)) return problem(403, "Request blocked.", "csrf");

  const declared = Number(request.headers.get("content-length") ?? "0");
  if (declared > MAX_BODY_BYTES) return problem(413, "The upload is too large.");

  let body: ArrayBuffer | undefined;
  try {
    body = request.method === "GET" || request.method === "HEAD" ? undefined : await readBoundedBody(request, MAX_BODY_BYTES);
  } catch (error) {
    if (error instanceof BodyTooLargeError) return problem(413, "The upload is too large.");
    return problem(400, "The request body could not be read.");
  }

  const search = new URL(request.url).search;
  const target = `${apiBaseUrl()}/api/${joined}${search}`;

  const send = (token: string | null) => {
    const headers: Record<string, string> = { ...forwardedFor(request) };
    for (const name of FORWARD_REQUEST_HEADERS) {
      const value = request.headers.get(name);
      if (value) headers[name] = value;
    }
    if (token) headers.authorization = `Bearer ${token}`;
    return fetch(target, { method: request.method, headers, body, cache: "no-store", redirect: "manual", signal: AbortSignal.timeout(90_000) });
  };

  let session: Awaited<ReturnType<typeof resolveAccessToken>>;
  try {
    session = await resolveAccessToken(request);
  } catch {
    return problem(503, "FarmHand is temporarily unavailable. Please try again shortly.", "api_unreachable");
  }
  let rotated: TokenPair | null = session.rotated;

  let upstream: Response;
  try {
    upstream = await send(session.token);

    // The access token can be revoked between our expiry check and the API call; refresh once and retry.
    if (upstream.status === 401 && session.token) {
      const refresh = (await cookies()).get(REFRESH_COOKIE)?.value;
      const next = refresh ? await refreshTokens(rotated?.refreshToken ?? refresh, request) : null;
      if (next) {
        rotated = next;
        upstream = await send(next.token);
      }
    }
  } catch {
    return problem(503, "FarmHand is temporarily unavailable. Please try again shortly.", "api_unreachable");
  }

  if (upstream.status === 401) {
    const response = NextResponse.json(
      { status: 401, title: "Your session has ended. Please sign in again.", code: "session_expired" },
      { status: 401, headers: { "cache-control": "no-store" } },
    );
    clearSessionCookies(response.cookies);
    return response;
  }

  const headers = new Headers({ "cache-control": "no-store" });
  for (const name of FORWARD_RESPONSE_HEADERS) {
    const value = upstream.headers.get(name);
    if (value && name !== "cache-control") headers.set(name, value);
  }

  const response = new NextResponse(upstream.status === 204 ? null : upstream.body, { status: upstream.status, headers });
  if (rotated) writeSessionCookies(response.cookies, rotated);
  return response;
}

export { handle as GET, handle as POST, handle as PUT, handle as DELETE, handle as PATCH };
