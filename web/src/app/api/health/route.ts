import { apiBaseUrl } from "@/lib/server/session";

export const dynamic = "force-dynamic";

export async function GET() {
  try {
    const api = await fetch(`${apiBaseUrl()}/health/ready`, {
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    });
    return Response.json(
      { status: api.ok ? "ready" : "unavailable" },
      { status: api.ok ? 200 : 503, headers: { "cache-control": "no-store" } },
    );
  } catch {
    return Response.json(
      { status: "unavailable" },
      { status: 503, headers: { "cache-control": "no-store" } },
    );
  }
}
