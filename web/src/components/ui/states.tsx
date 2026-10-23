"use client";

import { AlertTriangle, Lock, RefreshCw, SearchX, WifiOff } from "lucide-react";
import type { ReactNode } from "react";
import { ApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { Button } from "./button";

export function EmptyState({
  icon,
  title,
  children,
  action,
  className,
  image,
}: {
  icon?: ReactNode;
  title: string;
  children?: ReactNode;
  action?: ReactNode;
  className?: string;
  image?: ReactNode;
}) {
  return (
    <div className={cn("flex flex-col items-center rounded-[28px] bg-paper px-6 py-10 text-center ring-1 ring-line/60", className)}>
      {image}
      {icon && !image && <div className="mb-4 flex size-14 items-center justify-center rounded-2xl bg-sand text-ink [&_svg]:size-6">{icon}</div>}
      <h3 className="text-lg font-semibold tracking-[-0.01em] text-balance">{title}</h3>
      {children && <div className="mt-1.5 max-w-sm text-[15px] leading-relaxed text-muted text-pretty">{children}</div>}
      {action && <div className="mt-5 flex flex-wrap justify-center gap-2">{action}</div>}
    </div>
  );
}

/** Error panel that explains offline, permission, not-found and server failures differently. */
export function ErrorState({ error, onRetry, className, compact }: { error: unknown; onRetry?: () => void; className?: string; compact?: boolean }) {
  const apiError = error instanceof ApiError ? error : null;
  let icon = <AlertTriangle />;
  let title = "Something went wrong";
  let body = apiError?.userMessage ?? "Please try again.";

  if (apiError?.isNetwork) {
    icon = <WifiOff />;
    title = "You're offline";
    body = "We couldn't reach FarmHand. Check your connection — anything you were writing is still here.";
  } else if (apiError?.status === 403) {
    icon = <Lock />;
    title = "You don't have access";
    body = apiError.detail ?? "This belongs to another account, or your role can't open it.";
  } else if (apiError?.status === 404) {
    icon = <SearchX />;
    title = "Not found";
    body = apiError.detail ?? "It may have been removed, or the link is wrong.";
  }

  return (
    <div role="alert" className={cn("flex flex-col items-center rounded-[28px] bg-paper text-center ring-1 ring-line/60", compact ? "px-4 py-6" : "px-6 py-10", className)}>
      <div className="mb-3 flex size-12 items-center justify-center rounded-2xl bg-red-soft text-red-ink [&_svg]:size-5">{icon}</div>
      <h3 className="text-base font-semibold">{title}</h3>
      <p className="mt-1 max-w-sm text-sm leading-relaxed text-muted">{body}</p>
      {onRetry && apiError?.status !== 403 && apiError?.status !== 404 && (
        <Button variant="secondary" size="sm" className="mt-4" icon={<RefreshCw className="size-4" />} onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  );
}

export function InlineError({ error, className }: { error: unknown; className?: string }) {
  if (!error) return null;
  const message = error instanceof ApiError ? error.userMessage : error instanceof Error ? error.message : String(error);
  return (
    <div role="alert" className={cn("flex items-start gap-2.5 rounded-2xl bg-red-soft px-4 py-3 text-sm font-medium text-red-ink", className)}>
      <AlertTriangle className="mt-0.5 size-4 shrink-0" />
      <span>{message}</span>
    </div>
  );
}

export function Notice({ tone = "info", icon, title, children, className }: { tone?: "info" | "warning" | "success" | "demo"; icon?: ReactNode; title?: string; children: ReactNode; className?: string }) {
  const tones = {
    info: "bg-[#e9e6db] text-ink-3",
    warning: "bg-terracotta-soft text-terracotta-deep",
    success: "bg-green-soft text-green-ink",
    demo: "bg-olive-soft text-olive-ink",
  } as const;
  return (
    <div className={cn("flex items-start gap-3 rounded-2xl px-4 py-3 text-sm leading-relaxed", tones[tone], className)}>
      {icon && <span className="mt-0.5 shrink-0 [&_svg]:size-4">{icon}</span>}
      <div>
        {title && <p className="font-semibold">{title}</p>}
        <div>{children}</div>
      </div>
    </div>
  );
}
