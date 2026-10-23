"use client";

import { useState } from "react";
import { formatDate } from "@/lib/format";

type Day = { date: string; observations: number; deaths: number };

/**
 * Segmented arc (one rounded segment per day) showing which of the last 14 days had at least one
 * health check recorded. Filled = checked, track = no record. The centre carries the headline.
 */
export function CheckInArc({ days, caption }: { days: Day[]; caption?: string }) {
  const [hover, setHover] = useState<number | null>(null);
  const checked = days.filter((d) => d.observations > 0).length;
  const total = days.length;

  const size = 300;
  const cx = size / 2;
  const cy = 168;
  const radius = 118;
  const sweep = 220; // degrees
  const start = -110; // degrees from 12 o'clock
  const step = sweep / Math.max(1, total - 1);
  const active = hover !== null ? days[hover] : null;

  return (
    <figure className="relative mx-auto w-full max-w-[340px]" aria-label={`Health checks recorded on ${checked} of the last ${total} days`}>
      <svg viewBox={`0 0 ${size} 220`} className="w-full overflow-visible" role="img" aria-hidden="true">
        {days.map((day, i) => {
          const angle = start + i * step;
          const filled = day.observations > 0;
          return (
            <g
              key={day.date}
              transform={`rotate(${angle} ${cx} ${cy}) translate(${cx} ${cy - radius})`}
              onPointerEnter={() => setHover(i)}
              onPointerLeave={() => setHover((h) => (h === i ? null : h))}
            >
              <rect x={-17} y={-26} width={34} height={52} fill="transparent" />
              <rect
                x={-11}
                y={-22}
                width={22}
                height={44}
                rx={9}
                className="transition-[fill,opacity] duration-200"
                fill={filled ? "#C15935" : "#E2DBCD"}
                opacity={hover === null || hover === i ? 1 : 0.55}
              />
            </g>
          );
        })}
      </svg>
      <figcaption className="pointer-events-none absolute inset-x-0 top-[44%] flex flex-col items-center text-center">
        {active ? (
          <>
            <span className="text-[13px] font-semibold text-muted">{formatDate(active.date, { weekday: "short", day: "numeric", month: "short" })}</span>
            <span className="mt-1 text-[34px] font-semibold leading-none tracking-[-0.03em]">{active.observations}</span>
            <span className="mt-1 text-[13px] text-muted">
              {active.observations === 1 ? "report" : "reports"} · {active.deaths} {active.deaths === 1 ? "death" : "deaths"}
            </span>
          </>
        ) : (
          <>
            <span className="text-[13px] font-semibold text-muted">Last 14 days</span>
            <span className="mt-1 text-[44px] font-semibold leading-none tracking-[-0.04em]">
              {checked}
              <span className="text-[22px] text-faint">/{total}</span>
            </span>
            <span className="mt-1.5 text-[13px] font-semibold text-terracotta-ink">{caption ?? "days with a health check"}</span>
          </>
        )}
      </figcaption>
      <table className="sr-only">
        <caption>Health checks per day</caption>
        <thead>
          <tr>
            <th>Date</th>
            <th>Reports</th>
            <th>Deaths</th>
          </tr>
        </thead>
        <tbody>
          {days.map((d) => (
            <tr key={d.date}>
              <td>{formatDate(d.date)}</td>
              <td>{d.observations}</td>
              <td>{d.deaths}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
