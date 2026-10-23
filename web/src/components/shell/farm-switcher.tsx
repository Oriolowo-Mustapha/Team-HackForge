"use client";

import { Check, ChevronDown, MapPin, Plus } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { cn } from "@/lib/cn";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { Modal } from "@/components/ui/dialog";

/** Shows the farm being worked on; farmers with several farms can switch here. */
export function FarmSwitcher({ inverse = false, className }: { inverse?: boolean; className?: string }) {
  const { farm, farms, setFarm } = useActiveFarm();
  const [open, setOpen] = useState(false);
  if (!farm) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={cn(
          "flex min-w-0 items-center gap-2 rounded-2xl px-3 py-2 text-left transition-colors",
          inverse ? "bg-cream/8 text-cream hover:bg-cream/12" : "hover:bg-paper",
          className,
        )}
        aria-label={`Current farm: ${farm.displayName}. Change farm`}
      >
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[15px] font-semibold leading-tight">{farm.displayName}</span>
          <span className={cn("flex items-center gap-1 truncate text-xs font-medium", inverse ? "text-cream/60" : "text-muted")}>
            <MapPin className="size-3 shrink-0" aria-hidden />
            <span className="truncate">{farm.location}</span>
          </span>
        </span>
        <ChevronDown className={cn("size-4 shrink-0", inverse ? "text-cream/60" : "text-muted")} aria-hidden />
      </button>
      <Modal open={open} onOpenChange={setOpen} title="Your farms" description="Reports, animals and cases are shown for the farm you choose.">
        <ul className="space-y-2">
          {farms.map((f) => {
            const selected = f.id === farm.id;
            return (
              <li key={f.id}>
                <button
                  type="button"
                  onClick={() => {
                    setFarm(f.id);
                    setOpen(false);
                  }}
                  aria-pressed={selected}
                  className={cn(
                    "flex w-full items-center gap-3 rounded-2xl border px-4 py-3 text-left transition-colors",
                    selected ? "border-ink bg-ink text-cream" : "border-line bg-paper hover:bg-sand",
                  )}
                >
                  <span className="min-w-0 flex-1">
                    <span className="block font-semibold">{f.displayName}</span>
                    <span className={cn("block text-sm", selected ? "text-cream/70" : "text-muted")}>{f.location}</span>
                  </span>
                  {selected && <Check className="size-5" aria-hidden />}
                </button>
              </li>
            );
          })}
        </ul>
        <Link
          href="/setup?another=1"
          onClick={() => setOpen(false)}
          className="mt-3 flex items-center justify-center gap-2 rounded-2xl border border-dashed border-line px-4 py-3 text-sm font-semibold text-muted hover:bg-sand hover:text-ink"
        >
          <Plus className="size-4" aria-hidden /> Add another farm
        </Link>
      </Modal>
    </>
  );
}
