import type { ReactNode } from "react";
import { formatNumber } from "@/lib/format";

/** Horizontal bars with every category named in text (colour never carries meaning alone). */
export function LabeledBars({ rows, emptyLabel = "Nothing to show yet" }: { rows: { label: string; value: number; icon?: ReactNode; hint?: string }[]; emptyLabel?: string }) {
  const max = Math.max(1, ...rows.map((r) => r.value));
  const total = rows.reduce((s, r) => s + r.value, 0);
  if (total === 0) return <p className="py-2 text-sm text-faint">{emptyLabel}</p>;
  return (
    <ul className="space-y-3">
      {rows.map((row) => (
        <li key={row.label}>
          <div className="mb-1.5 flex items-center justify-between gap-3 text-sm">
            <span className="flex items-center gap-2 font-medium text-ink">
              {row.icon}
              {row.label}
              {row.hint && <span className="text-xs font-normal text-faint">{row.hint}</span>}
            </span>
            <span className="font-semibold tabular">{formatNumber(row.value)}</span>
          </div>
          <div className="h-2 rounded-full bg-sand" aria-hidden>
            <div className="h-2 rounded-full bg-ink-3" style={{ width: `${row.value === 0 ? 0 : Math.max(3, (row.value / max) * 100)}%` }} />
          </div>
        </li>
      ))}
    </ul>
  );
}
