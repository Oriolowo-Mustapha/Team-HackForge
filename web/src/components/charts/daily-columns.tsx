"use client";

import { useState } from "react";
import { cn } from "@/lib/cn";
import { formatDate } from "@/lib/format";

type Point = { date: string; value: number };

/** Single-series column chart (e.g. deaths per day). One neutral colour; the peak is labelled; values on hover/focus; table for screen readers. */
export function DailyColumns({ points, label, unit }: { points: Point[]; label: string; unit: [string, string] }) {
  const [active, setActive] = useState<number | null>(null);
  const max = Math.max(1, ...points.map((p) => p.value));
  const peakIndex = points.reduce((best, p, i) => (p.value > (points[best]?.value ?? -1) ? i : best), 0);
  const total = points.reduce((sum, p) => sum + p.value, 0);

  return (
    <figure aria-label={label} className="w-full">
      <div className="relative">
        <div className="flex h-28 items-end gap-[2px] border-b border-line">
          {points.map((p, i) => {
            const height = p.value === 0 ? 0 : Math.max(6, (p.value / max) * 100);
            const isActive = active === i;
            return (
              <div
                key={p.date}
                tabIndex={0}
                role="img"
                aria-label={`${formatDate(p.date, { day: "numeric", month: "short" })}: ${p.value} ${p.value === 1 ? unit[0] : unit[1]}`}
                onPointerEnter={() => setActive(i)}
                onPointerLeave={() => setActive((a) => (a === i ? null : a))}
                onFocus={() => setActive(i)}
                onBlur={() => setActive((a) => (a === i ? null : a))}
                className="relative flex h-full flex-1 cursor-default items-end justify-center rounded-md outline-none focus-visible:bg-sand"
              >
                {i === peakIndex && p.value > 0 && active === null && (
                  <span className="absolute text-[11px] font-semibold text-muted tabular" style={{ bottom: `calc(${height}% + 4px)` }}>
                    {p.value}
                  </span>
                )}
                <span
                  className={cn("block w-full max-w-[18px] rounded-t-[4px] transition-colors", isActive ? "bg-ink" : "bg-muted/80")}
                  style={{ height: `${height}%` }}
                />
              </div>
            );
          })}
        </div>
        {active !== null && points[active] && (
          <div
            role="status"
            className="pointer-events-none absolute -top-2 z-10 -translate-x-1/2 -translate-y-full rounded-xl bg-ink px-3 py-2 text-cream shadow-lift"
            style={{ left: `${((active + 0.5) / points.length) * 100}%` }}
          >
            <div className="text-base font-semibold leading-none tabular">{points[active].value}</div>
            <div className="mt-1 whitespace-nowrap text-[11px] text-cream/75">
              {points[active].value === 1 ? unit[0] : unit[1]} · {formatDate(points[active].date, { weekday: "short", day: "numeric", month: "short" })}
            </div>
          </div>
        )}
      </div>
      <div className="mt-2 flex justify-between text-[11px] font-medium text-faint tabular">
        <span>{points[0] ? formatDate(points[0].date, { day: "numeric", month: "short" }) : ""}</span>
        <span>{total} in total</span>
        <span>Today</span>
      </div>
      <table className="sr-only">
        <caption>{label}</caption>
        <tbody>
          {points.map((p) => (
            <tr key={p.date}>
              <td>{formatDate(p.date)}</td>
              <td>{p.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
