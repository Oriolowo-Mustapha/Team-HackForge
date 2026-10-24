import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { REFRESH_COOKIE, apiBaseUrl, clearSessionCookies, forwardedFor, isTrustedMutation, problem, resolveAccessToken } from "@/lib/server/session";

export const dynamic = "force-dynamic";

/**
 * Signs out. `{ everywhere: true }` also revokes every other session for the account.
 * Cookies are always cleared, even if the API cannot be reached.
 */
export async function POST(request: Request) {
  if (!isTrustedMutation(request)) return problem(403, "Request blocked.", "csrf");

  const store = await cookies();
  let refresh = store.get(REFRESH_COOKIE)?.value;
  const everywhere = await request.json().then((b: { everywhere?: boolean }) => b?.everywhere === true).catch(() => false);
  const headers = { "content-type": "application/json", ...forwardedFor(request) };

  let everywhereSignedOut = false;
  if (everywhere) {
    try {
      const session = await resolveAccessToken(request);
      refresh = session.rotated?.refreshToken ?? refresh;
      if (session.token) {
        const revoked = await fetch(`${apiBaseUrl()}/api/auth/revoke`, { method: "POST", headers: { ...headers, authorization: `Bearer ${session.token}` }, cache: "no-store", signal: AbortSignal.timeout(15_000) });
        everywhereSignedOut = revoked.ok;
      }
    } catch {
      // The response distinguishes current-device sign-out from unconfirmed global revocation.
    }
  }
  try {
    if (refresh) {
      await fetch(`${apiBaseUrl()}/api/auth/logout`, { method: "POST", headers, body: JSON.stringify({ refreshToken: refresh }), cache: "no-store", signal: AbortSignal.timeout(15_000) });
    }
  } catch {
    // Signing out locally must still succeed when the API is unreachable.
  }

  const response = NextResponse.json({ signedOut: true, everywhereSignedOut }, { headers: { "cache-control": "no-store", "clear-site-data": '"cache"' } });
  clearSessionCookies(response.cookies);
  return response;
}
