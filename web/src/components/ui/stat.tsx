import Link from "next/link";
import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

/** Small stat tile: label, value and optional context line. */
export function Stat({ label, value, context, icon, href, emphasis, className }: { label: string; value: ReactNode; context?: ReactNode; icon?: ReactNode; href?: string; emphasis?: boolean; className?: string }) {
  const body = (
    <>
      <div className="flex min-w-0 items-center justify-between gap-2">
        <span className={cn("min-w-0 text-[13px] font-semibold", emphasis ? "text-cream/75" : "text-muted")}>{label}</span>
        {icon && <span className={cn("shrink-0 [&_svg]:size-4", emphasis ? "text-cream/70" : "text-faint")}>{icon}</span>}
      </div>
      <div className="mt-2 break-words text-[22px] font-semibold leading-none tracking-[-0.03em] sm:text-[28px]">{value}</div>
      {context && <div className={cn("mt-1.5 text-xs font-medium", emphasis ? "text-cream/70" : "text-faint")}>{context}</div>}
    </>
  );
  const classes = cn(
    "block min-w-0 rounded-[24px] p-4 ring-1 transition-colors",
    emphasis ? "bg-ink text-cream ring-ink" : "bg-paper ring-line/60",
    href && (emphasis ? "hover:bg-ink-2" : "hover:bg-white"),
    className,
  );
  return href ? (
    <Link href={href} className={classes}>
      {body}
    </Link>
  ) : (
    <div className={classes}>{body}</div>
  );
}
