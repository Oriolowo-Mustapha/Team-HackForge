import { NextResponse, type NextRequest } from "next/server";
import { homeFor } from "@/lib/roles";

/**
 * Optimistic routing only: redirects signed-out visitors away from app pages and sends each role to
 * its own area. Real authorization is always enforced by the FarmHand API on every request.
 */

const PUBLIC_PATHS = ["/", "/login", "/register", "/forgot-password", "/reset-password", "/offline", "/about"];
const AUTH_PAGES = ["/login", "/register"];
const ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

function roleFrom(token: string | undefined): string | null {
  const part = token?.split(".")[1];
  if (!part) return null;
  try {
    const json = JSON.parse(atob(part.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(part.length / 4) * 4, "=")));
    const role = json.role ?? json[ROLE_CLAIM];
    return typeof role === "string" ? role : null;
  } catch {
    return null;
  }
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const hasSession = Boolean(request.cookies.get("fh_refresh")?.value);
  const role = roleFrom(request.cookies.get("fh_access")?.value);

  const isPublic = PUBLIC_PATHS.includes(pathname);

  if (!hasSession && !isPublic && pathname !== "/change-password") {
    const url = new URL("/login", request.url);
    url.searchParams.set("next", `${pathname}${search}`);
    return NextResponse.redirect(url);
  }

  if (hasSession && (pathname === "/" || AUTH_PAGES.includes(pathname))) {
    return NextResponse.redirect(new URL(homeFor(role), request.url));
  }

  if (hasSession && role) {
    if (pathname.startsWith("/admin") && role !== "Admin") return NextResponse.redirect(new URL(homeFor(role), request.url));
    if (pathname.startsWith("/vet") && !pathname.startsWith("/vets") && role !== "Vet") return NextResponse.redirect(new URL(homeFor(role), request.url));
    if (pathname === "/home" && role !== "Farmer") return NextResponse.redirect(new URL(homeFor(role), request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    // Skip API routes, Next internals and static assets.
    "/((?!api|_next|images|icons|brand|sw.js|manifest.webmanifest|favicon.ico|robots.txt).*)",
  ],
};
