import { NextResponse } from "next/server";
import { BodyTooLargeError, readBoundedBody } from "@/lib/server/request-body";
import { apiBaseUrl, forwardedFor, isTrustedMutation, problem, writeSessionCookies, type LoginPayload } from "@/lib/server/session";

export const dynamic = "force-dynamic";

/** Signs in through the API and stores the tokens in HttpOnly cookies. Tokens are never returned to the browser. */
export async function POST(request: Request) {
  if (!isTrustedMutation(request)) return problem(403, "Request blocked.", "csrf");

  let body: { email?: unknown; password?: unknown };
  try {
    body = JSON.parse(new TextDecoder().decode(await readBoundedBody(request, 16 * 1024)));
  } catch (error) {
    if (error instanceof BodyTooLargeError) return problem(413, "The request is too large.");
    return problem(400, "Invalid request.");
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${apiBaseUrl()}/api/auth/login`, {
      method: "POST",
      headers: { "content-type": "application/json", ...forwardedFor(request) },
      body: JSON.stringify({ email: body.email, password: body.password }),
      cache: "no-store", signal: AbortSignal.timeout(15_000),
    });
  } catch {
    return problem(503, "FarmHand is temporarily unavailable. Please try again shortly.", "api_unreachable");
  }

  if (!upstream.ok) {
    const text = await upstream.text();
    return new Response(text, {
      status: upstream.status,
      headers: { "content-type": upstream.headers.get("content-type") ?? "application/json", "cache-control": "no-store" },
    });
  }

  const session = (await upstream.json()) as LoginPayload;
  const response = NextResponse.json(
    {
      userId: session.userId,
      email: session.email,
      role: session.role,
      fullName: session.fullName,
      mustChangePassword: session.mustChangePassword,
    },
    { headers: { "cache-control": "no-store" } },
  );
  writeSessionCookies(response.cookies, session);
  return response;
}
