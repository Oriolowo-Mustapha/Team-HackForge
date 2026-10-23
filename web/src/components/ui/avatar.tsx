import { cn } from "@/lib/cn";
import { initials } from "@/lib/format";

export function Avatar({ name, className, tone = "beige" }: { name: string | null | undefined; className?: string; tone?: "beige" | "ink" | "terracotta" }) {
  return (
    <span
      aria-hidden
      className={cn(
        "inline-flex size-10 shrink-0 items-center justify-center rounded-full text-sm font-bold tracking-tight",
        tone === "beige" && "bg-beige text-ink",
        tone === "ink" && "bg-ink text-cream",
        tone === "terracotta" && "bg-terracotta text-white",
        className,
      )}
    >
      {initials(name)}
    </span>
  );
}
