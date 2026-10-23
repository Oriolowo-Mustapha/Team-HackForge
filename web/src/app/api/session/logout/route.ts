import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { ACCESS_COOKIE, REFRESH_COOKIE, apiBaseUrl, clearSessionCookies, forwardedFor, isTrustedMutation, problem } from "@/lib/server/session";

export const dynamic = "force-dynamic";

/**
 * Signs out. `{ everywhere: true }` also revokes every other session for the account.
 * Cookies are always cleared, even if the API cannot be reached.
 */
export async function POST(request: Request) {
  if (!isTrustedMutation(request)) return problem(403, "Request blocked.", "csrf");

  const store = await cookies();
  const access = store.get(ACCESS_COOKIE)?.value;
  const refresh = store.get(REFRESH_COOKIE)?.value;
  const everywhere = await request.json().then((b: { everywhere?: boolean }) => b?.everywhere === true).catch(() => false);
  const headers = { "content-type": "application/json", ...forwardedFor(request) };

  try {
    if (everywhere && access) {
      await fetch(`${apiBaseUrl()}/api/auth/revoke`, { method: "POST", headers: { ...headers, authorization: `Bearer ${access}` }, cache: "no-store" });
    }
    if (refresh) {
      await fetch(`${apiBaseUrl()}/api/auth/logout`, { method: "POST", headers, body: JSON.stringify({ refreshToken: refresh }), cache: "no-store" });
    }
  } catch {
    // Signing out locally must still succeed when the API is unreachable.
  }

  const response = NextResponse.json({ signedOut: true }, { headers: { "cache-control": "no-store", "clear-site-data": '"cache"' } });
  clearSessionCookies(response.cookies);
  return response;
}
