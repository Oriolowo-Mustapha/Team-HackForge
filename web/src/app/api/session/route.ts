import { NextResponse } from "next/server";
import { apiBaseUrl, clearSessionCookies, forwardedFor, resolveAccessToken, writeSessionCookies } from "@/lib/server/session";

export const dynamic = "force-dynamic";

/** Returns the signed-in user's profile (refreshing the session if needed), or `{ authenticated: false }`. */
export async function GET(request: Request) {
  const { token, rotated, expired } = await resolveAccessToken(request);
  if (!token) {
    const response = NextResponse.json({ authenticated: false, expired }, { headers: { "cache-control": "no-store" } });
    if (expired) clearSessionCookies(response.cookies);
    return response;
  }

  let me: Response;
  try {
    me = await fetch(`${apiBaseUrl()}/api/auth/me`, {
      headers: { authorization: `Bearer ${token}`, ...forwardedFor(request) },
      cache: "no-store",
    });
  } catch {
    return NextResponse.json({ status: 503, title: "The FarmHand service is unreachable.", code: "api_unreachable" }, { status: 503 });
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
