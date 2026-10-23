"use client";

import { ArrowLeft } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { CountBadge } from "./badge";

export function PageHeader({
  title,
  subtitle,
  back,
  actions,
  eyebrow,
  className,
}: {
  title: ReactNode;
  subtitle?: ReactNode;
  back?: string | true;
  actions?: ReactNode;
  eyebrow?: ReactNode;
  className?: string;
}) {
  const router = useRouter();
  return (
    <header className={cn("mb-5 flex items-start gap-3", className)}>
      {back &&
        (typeof back === "string" ? (
          <Link href={back} aria-label="Back" className="inline-flex size-11 shrink-0 items-center justify-center rounded-2xl bg-paper shadow-soft hover:bg-white">
            <ArrowLeft className="size-5" />
          </Link>
        ) : (
          <button type="button" onClick={() => router.back()} aria-label="Back" className="inline-flex size-11 shrink-0 items-center justify-center rounded-2xl bg-paper shadow-soft hover:bg-white">
            <ArrowLeft className="size-5" />
          </button>
        ))}
      <div className="min-w-0 flex-1 pt-0.5">
        {eyebrow && <div className="mb-1 text-[13px] font-semibold text-muted">{eyebrow}</div>}
        <h1 className="text-[26px] font-semibold leading-[1.15] tracking-[-0.025em] text-balance sm:text-[30px]">{title}</h1>
        {subtitle && <p className="mt-1.5 text-[15px] leading-relaxed text-muted text-pretty">{subtitle}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </header>
  );
}

export function SectionHeader({ title, count, href, linkLabel = "See all", className, action }: { title: string; count?: number; href?: string; linkLabel?: string; className?: string; action?: ReactNode }) {
  return (
    <div className={cn("mb-3 flex items-center justify-between gap-3", className)}>
      <h2 className="flex items-center gap-2 text-[19px] font-semibold tracking-[-0.02em]">
        {title}
        {typeof count === "number" && count > 0 && <CountBadge count={count} />}
      </h2>
      {action}
      {href && (
        <Link href={href} className="rounded-lg px-1 py-1 text-sm font-semibold text-muted hover:text-ink">
          {linkLabel}
        </Link>
      )}
    </div>
  );
}
