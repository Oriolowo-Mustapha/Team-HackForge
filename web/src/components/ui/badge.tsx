import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import type { Tone } from "@/lib/api/enums";

const tones: Record<Tone, { pill: string; dot: string }> = {
  healthy: { pill: "bg-green-soft text-green-ink", dot: "bg-green" },
  pending: { pill: "bg-olive-soft text-olive-ink", dot: "bg-olive" },
  concerning: { pill: "bg-terracotta-soft text-terracotta-deep", dot: "bg-terracotta" },
  urgent: { pill: "bg-terracotta-ink text-white", dot: "bg-white" },
  critical: { pill: "bg-red-ink text-white", dot: "bg-white" },
  confirmed: { pill: "bg-ink text-cream", dot: "bg-cream" },
  failed: { pill: "bg-red-soft text-red-ink", dot: "bg-red" },
  neutral: { pill: "bg-sand text-muted", dot: "bg-taupe" },
  info: { pill: "bg-[#e6e3d6] text-ink-3", dot: "bg-ink-3" },
};

export function Badge({ tone = "neutral", children, icon, className, dot = false }: { tone?: Tone; children: ReactNode; icon?: ReactNode; className?: string; dot?: boolean }) {
  const t = tones[tone];
  return (
    <span className={cn("inline-flex h-6 shrink-0 items-center gap-1.5 rounded-full px-2.5 text-xs font-semibold whitespace-nowrap", t.pill, className)}>
      {dot && <span aria-hidden className={cn("size-1.5 rounded-full", t.dot)} />}
      {icon}
      {children}
    </span>
  );
}

export function CountBadge({ count, className }: { count: number; className?: string }) {
  return (
    <span className={cn("inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-terracotta px-1.5 text-[11px] font-bold text-white tabular", className)}>
      {count > 99 ? "99+" : count}
    </span>
  );
}
