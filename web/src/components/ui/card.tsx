import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/cn";

export function Card({ className, padded = true, ...props }: HTMLAttributes<HTMLDivElement> & { padded?: boolean }) {
  return <div className={cn("rounded-[28px] bg-paper shadow-soft ring-1 ring-line/60", padded && "p-5", className)} {...props} />;
}

export function CardTitle({ children, className, action }: { children: ReactNode; className?: string; action?: ReactNode }) {
  return (
    <div className={cn("mb-3 flex items-center justify-between gap-3", className)}>
      <h2 className="text-[17px] font-semibold tracking-[-0.01em]">{children}</h2>
      {action}
    </div>
  );
}
