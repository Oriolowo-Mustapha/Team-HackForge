"use client";

import { Lock, LogOut } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { firstName } from "@/lib/format";
import { useSession } from "@/lib/hooks/use-session";
import { useSignOut } from "@/lib/hooks/use-sign-out";
import { homeFor, type Role } from "@/lib/roles";
import type { SessionUser } from "@/lib/api/schemas";
import { Avatar } from "@/components/ui/avatar";
import { ButtonLink } from "@/components/ui/button";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Spinner } from "@/components/ui/spinner";
import { DemoBanner, OfflineBanner } from "./banners";
import { Brand, BrandMark } from "./brand";
import { FarmSwitcher } from "./farm-switcher";
import { navByRole, type NavItem } from "./nav-config";
import { NotificationsButton } from "./notifications";

const FARMER_ONLY = ["/home", "/animals", "/scan", "/reports", "/setup", "/pay", "/vets"];
const under = (path: string, prefix: string) => path === prefix || path.startsWith(`${prefix}/`);

/** Which roles may open a path. The API enforces the real rules; this keeps navigation coherent. */
function rolesFor(path: string): Role[] | null {
  if (under(path, "/admin")) return ["Admin"];
  if (under(path, "/vet")) return ["Vet"];
  if (FARMER_ONLY.some((p) => under(path, p))) return ["Farmer"];
  return null;
}

/** Pages that take over the screen (no navigation chrome). */
const FOCUS_PATHS = ["/setup", "/scan"];

export function FullScreenLoader({ label = "Loading FarmHand" }: { label?: string }) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-5 bg-cream" role="status" aria-live="polite">
      <BrandMark className="size-14 animate-pulse" />
      <Spinner className="size-5 text-muted" />
      <span className="sr-only">{label}</span>
    </div>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const { user, status, isError, error, refetch } = useSession();
  const pathname = usePathname();
  const router = useRouter();

  const needsFarm = user?.role === "Farmer" && user.farms.length === 0;

  useEffect(() => {
    if (status === "anonymous" && !isError) router.replace(`/login?next=${encodeURIComponent(pathname)}`);
    else if (user?.mustChangePassword) router.replace("/change-password");
    else if (needsFarm && pathname !== "/setup") router.replace("/setup");
  }, [status, isError, user, needsFarm, pathname, router]);

  if (isError && !user) {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-cream p-4">
        <ErrorState error={error} onRetry={() => void refetch()} className="w-full max-w-md" />
      </div>
    );
  }
  if (!user || user.mustChangePassword || (needsFarm && pathname !== "/setup")) return <FullScreenLoader />;

  const allowed = rolesFor(pathname);
  const forbidden = allowed !== null && !allowed.includes(user.role);
  const focus = FOCUS_PATHS.some((p) => under(pathname, p)) && !forbidden;

  if (focus) {
    return (
      <div className="min-h-dvh bg-cream">
        <OfflineBanner />
        {children}
      </div>
    );
  }

  const nav = navByRole[user.role];

  return (
    <div className="min-h-dvh bg-cream">
      <a href="#main" className="sr-only z-[60] rounded-xl bg-ink px-4 py-2 text-cream focus:not-sr-only focus:fixed focus:left-3 focus:top-3">
        Skip to content
      </a>

      <Sidebar user={user} pathname={pathname} items={nav.desktop} primary={nav.primaryAction} />

      <div className="lg:pl-[276px]">
        <DemoBanner />
        <OfflineBanner />

        <header className="flex items-center gap-2 px-4 pb-1 pt-[max(12px,env(safe-area-inset-top))] sm:px-6 lg:hidden">
          <Link href={homeFor(user.role)} aria-label="FarmHand home" className="rounded-xl">
            <BrandMark className="size-10" />
          </Link>
          <div className="min-w-0 flex-1">
            {user.role === "Farmer" ? (
              <FarmSwitcher />
            ) : (
              <div className="px-3">
                <p className="truncate text-[15px] font-semibold leading-tight">{user.role === "Vet" ? firstName(user.fullName, "Doctor") : "Administration"}</p>
                <p className="text-xs font-medium text-muted">{user.role === "Vet" ? user.vetProfile?.specialization ?? "Veterinarian" : "FarmHand platform"}</p>
              </div>
            )}
          </div>
          <NotificationsButton user={user} />
          <Link href="/profile" aria-label="Profile and settings" className="rounded-full">
            <Avatar name={user.fullName ?? user.email} className="size-11" tone={user.role === "Farmer" ? "beige" : "ink"} />
          </Link>
        </header>

        <div className="mx-auto hidden max-w-[1180px] items-center justify-end gap-2 px-10 pt-5 lg:flex">
          {user.role === "Farmer" && <FarmSwitcher className="mr-auto -ml-3" />}
          <NotificationsButton user={user} />
        </div>

        <main id="main" className="mx-auto w-full max-w-[1180px] px-4 pb-[calc(112px+env(safe-area-inset-bottom))] pt-3 sm:px-6 lg:px-10 lg:pb-16 lg:pt-4">
          {forbidden ? (
            <EmptyState
              icon={<Lock />}
              title="This area isn't part of your account"
              action={
                <ButtonLink href={homeFor(user.role)} variant="primary">
                  Go to my dashboard
                </ButtonLink>
              }
            >
              {user.role === "Farmer"
                ? "Vet and admin tools are only available to verified veterinarians and administrators."
                : "This page is for farmers managing their own farm."}
            </EmptyState>
          ) : (
            children
          )}
        </main>
      </div>

      <BottomNav pathname={pathname} items={nav.mobile} />
    </div>
  );
}

function isActive(item: NavItem, pathname: string) {
  return item.match ? item.match(pathname) : pathname === item.href;
}

function Sidebar({ user, pathname, items, primary }: { user: SessionUser; pathname: string; items: NavItem[]; primary?: NavItem }) {
  const { signOut, pending } = useSignOut();
  return (
    <aside className="fixed inset-y-3 left-3 z-30 hidden w-[256px] flex-col rounded-[30px] bg-ink p-3 text-cream shadow-lift lg:flex" aria-label="Sidebar">
      <div className="px-3 pb-6 pt-4">
        <Brand href={homeFor(user.role)} inverse />
      </div>

      {primary && (
        <Link
          href={primary.href}
          className="mb-5 flex h-12 items-center justify-center gap-2 rounded-2xl bg-terracotta-ink text-[15px] font-semibold text-white shadow-soft transition-colors hover:bg-terracotta-deep"
        >
          <primary.icon className="size-5" aria-hidden />
          {primary.label}
        </Link>
      )}

      <nav aria-label="Main" className="scrollbar-none -mx-1 flex-1 overflow-y-auto px-1">
        <ul className="space-y-1">
          {items.map((item) => {
            const active = isActive(item, pathname);
            return (
              <li key={item.href}>
                <Link
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  className={cn(
                    "group flex h-11 items-center gap-3 rounded-2xl px-3 text-[14.5px] font-semibold transition-colors",
                    active ? "bg-cream/10 text-cream" : "text-cream/60 hover:bg-cream/5 hover:text-cream",
                  )}
                >
                  <span
                    className={cn(
                      "flex size-8 items-center justify-center rounded-xl transition-colors",
                      active ? "bg-terracotta text-white" : "text-cream/60 group-hover:text-cream",
                    )}
                  >
                    <item.icon className="size-[18px]" aria-hidden />
                  </span>
                  {item.label}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      <div className="mt-4 rounded-[22px] bg-cream/6 p-3">
        <div className="flex items-center gap-3">
          <Avatar name={user.fullName ?? user.email} className="size-10" tone="beige" />
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-semibold">{user.fullName ?? user.email}</p>
            <p className="truncate text-xs text-cream/55">{user.role === "Vet" ? "Veterinarian" : user.role}</p>
          </div>
          <button
            type="button"
            onClick={() => void signOut()}
            disabled={pending}
            className="inline-flex size-9 items-center justify-center rounded-xl text-cream/60 hover:bg-cream/10 hover:text-cream disabled:opacity-50"
            aria-label="Sign out"
            title="Sign out"
          >
            {pending ? <Spinner className="size-4" /> : <LogOut className="size-[18px]" aria-hidden />}
          </button>
        </div>
      </div>
    </aside>
  );
}

function BottomNav({ pathname, items }: { pathname: string; items: NavItem[] }) {
  return (
    <nav
      aria-label="Main"
      className="fixed inset-x-3 bottom-[max(12px,env(safe-area-inset-bottom))] z-40 mx-auto max-w-[520px] rounded-[30px] bg-ink px-2 py-2 shadow-nav lg:hidden"
    >
      <ul className="flex items-end justify-between">
        {items.map((item) => {
          const active = isActive(item, pathname);
          if (item.primary) {
            return (
              <li key={item.href} className="flex flex-1 justify-center">
                <Link href={item.href} aria-label="Report a health concern" className="group -mt-8 flex flex-col items-center gap-1">
                  <span className="flex size-[60px] items-center justify-center rounded-full bg-terracotta text-white shadow-lift ring-[5px] ring-cream transition-transform group-active:scale-95">
                    <item.icon className="size-7" aria-hidden />
                  </span>
                  <span className="text-[11px] font-semibold text-cream">{item.label}</span>
                </Link>
              </li>
            );
          }
          return (
            <li key={item.href} className="flex flex-1 justify-center">
              <Link
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={cn("flex min-w-[56px] flex-col items-center gap-1 rounded-2xl px-1 py-1 transition-colors", active ? "text-cream" : "text-cream/55 hover:text-cream")}
              >
                <span className={cn("flex h-8 w-12 items-center justify-center rounded-full transition-colors", active && "bg-terracotta text-white")}>
                  <item.icon className="size-[20px]" aria-hidden />
                </span>
                <span className="text-[11px] font-semibold">{item.label}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
