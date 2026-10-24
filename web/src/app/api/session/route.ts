import { NextResponse } from "next/server";
import { apiBaseUrl, clearSessionCookies, forwardedFor, resolveAccessToken, writeSessionCookies } from "@/lib/server/session";

export const dynamic = "force-dynamic";

/** Returns the signed-in user's profile (refreshing the session if needed), or `{ authenticated: false }`. */
export async function GET(request: Request) {
  let session: Awaited<ReturnType<typeof resolveAccessToken>>;
  try {
    session = await resolveAccessToken(request);
  } catch {
    return NextResponse.json({ status: 503, title: "FarmHand is temporarily unavailable.", code: "api_unreachable" }, { status: 503, headers: { "cache-control": "no-store" } });
  }
  const { token, rotated, expired } = session;
  if (!token) {
    const response = NextResponse.json({ authenticated: false, expired }, { headers: { "cache-control": "no-store" } });
    if (expired) clearSessionCookies(response.cookies);
    return response;
  }

  let me: Response;
  try {
    me = await fetch(`${apiBaseUrl()}/api/auth/me`, {
      headers: { authorization: `Bearer ${token}`, ...forwardedFor(request) },
      cache: "no-store", signal: AbortSignal.timeout(15_000),
    });
  } catch {
    return NextResponse.json({ status: 503, title: "FarmHand is temporarily unavailable. Please try again shortly.", code: "api_unreachable" }, { status: 503 });
  }

  if (me.status === 401) {
    const response = NextResponse.json({ authenticated: false, expired: true }, { headers: { "cache-control": "no-store" } });
    clearSessionCookies(response.cookies);
    return response;
  }

  if (!me.ok) {
    return NextResponse.json({ status: me.status, title: "Could not load your account." }, { status: me.status });
  }

  const response = NextResponse.json({ authenticated: true, user: await me.json() }, { headers: { "cache-control": "no-store" } });
  if (rotated) writeSessionCookies(response.cookies, rotated);
  return response;
}
