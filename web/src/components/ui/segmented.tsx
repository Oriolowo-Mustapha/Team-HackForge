"use client";

import { cn } from "@/lib/cn";

/** Single-select filter tabs. Rendered as a radio group so arrow keys and screen readers behave. */
export function Segmented<T extends string>({
  options,
  value,
  onChange,
  label,
  className,
}: {
  options: { value: T; label: string; count?: number }[];
  value: T;
  onChange: (value: T) => void;
  label: string;
  className?: string;
}) {
  return (
    <div role="radiogroup" aria-label={label} className={cn("scrollbar-none -mx-1 flex gap-1.5 overflow-x-auto px-1 pb-1", className)}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={selected}
            onClick={() => onChange(option.value)}
            className={cn(
              "inline-flex h-10 shrink-0 items-center gap-1.5 rounded-full px-4 text-sm font-semibold transition-colors",
              selected ? "bg-ink text-cream" : "bg-paper text-ink ring-1 ring-line hover:bg-sand",
            )}
          >
            {option.label}
            {typeof option.count === "number" && (
              <span className={cn("tabular text-xs", selected ? "text-cream/70" : "text-faint")}>{option.count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}
