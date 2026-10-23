import { NextResponse } from "next/server";
import { apiBaseUrl, forwardedFor, isTrustedMutation, problem, writeSessionCookies, type LoginPayload } from "@/lib/server/session";

export const dynamic = "force-dynamic";

/** Signs in through the API and stores the tokens in HttpOnly cookies. Tokens are never returned to the browser. */
export async function POST(request: Request) {
  if (!isTrustedMutation(request)) return problem(403, "Request blocked.", "csrf");

  let body: { email?: unknown; password?: unknown };
  try {
    body = await request.json();
  } catch {
    return problem(400, "Invalid request.");
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${apiBaseUrl()}/api/auth/login`, {
      method: "POST",
      headers: { "content-type": "application/json", ...forwardedFor(request) },
      body: JSON.stringify({ email: body.email, password: body.password }),
      cache: "no-store",
    });
  } catch {
    return problem(503, "The FarmHand service is unreachable. Check your connection and try again.", "api_unreachable");
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
