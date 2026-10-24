import { NextResponse } from "next/server";
import { BodyTooLargeError, readBoundedBody } from "@/lib/server/request-body";
import { apiBaseUrl, forwardedFor, isTrustedMutation, problem, writeSessionCookies, type LoginPayload } from "@/lib/server/session";

export const dynamic = "force-dynamic";

/** Creates a farmer account, then signs in so onboarding can continue straight away. */
export async function POST(request: Request) {
  if (!isTrustedMutation(request)) return problem(403, "Request blocked.", "csrf");

  let body: { email?: string; password?: string; fullName?: string; phoneNumber?: string };
  try {
    body = JSON.parse(new TextDecoder().decode(await readBoundedBody(request, 16 * 1024)));
  } catch (error) {
    if (error instanceof BodyTooLargeError) return problem(413, "The request is too large.");
    return problem(400, "Invalid request.");
  }

  const headers = { "content-type": "application/json", ...forwardedFor(request) };
  try {
    const registered = await fetch(`${apiBaseUrl()}/api/auth/register`, {
      method: "POST",
      headers,
      body: JSON.stringify({
        email: body.email,
        password: body.password,
        fullName: body.fullName || null,
        phoneNumber: body.phoneNumber || null,
      }),
      cache: "no-store", signal: AbortSignal.timeout(15_000),
    });
    if (!registered.ok) {
      return new Response(await registered.text(), {
        status: registered.status,
        headers: { "content-type": registered.headers.get("content-type") ?? "application/json" },
      });
    }

    const login = await fetch(`${apiBaseUrl()}/api/auth/login`, {
      method: "POST",
      headers,
      body: JSON.stringify({ email: body.email, password: body.password }),
      cache: "no-store", signal: AbortSignal.timeout(15_000),
    });
    if (!login.ok) return problem(500, "Your account was created, but signing in failed. Please sign in.");

    const session = (await login.json()) as LoginPayload;
    const response = NextResponse.json(
      { userId: session.userId, email: session.email, role: session.role, fullName: session.fullName, mustChangePassword: false },
      { status: 201, headers: { "cache-control": "no-store" } },
    );
    writeSessionCookies(response.cookies, session);
    return response;
  } catch {
    return problem(503, "FarmHand is temporarily unavailable. Please try again shortly.", "api_unreachable");
  }
}
