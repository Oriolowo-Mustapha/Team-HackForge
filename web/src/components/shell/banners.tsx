"use client";

import { FlaskConical, WifiOff } from "lucide-react";
import { useSystemInfo } from "@/lib/hooks/use-session";
import { useOnline } from "@/lib/hooks/use-online";

/** Persistent, honest labelling of what is live and what is simulated on this server. */
export function DemoBanner() {
  const { data } = useSystemInfo();
  if (!data?.demoMode) return null;
  const parts = [
    "Demo data",
    data.paymentsMode === "Simulated" ? "simulated payments (no money moves)" : data.paymentsMode === "Live" ? "live payments" : "payments off",
    data.aiTextConfigured ? "live AI" : data.ruleBasedDemoFallbackEnabled ? "rule-based analysis (no AI provider configured)" : "AI unavailable",
  ];
  return (
    <div className="flex items-center justify-center gap-2 bg-olive-soft px-4 py-1.5 text-center text-xs font-semibold text-olive-ink">
      <FlaskConical className="size-3.5 shrink-0" aria-hidden />
      <span>{parts.join(" · ")}</span>
    </div>
  );
}

export function OfflineBanner() {
  const online = useOnline();
  if (online) return null;
  return (
    <div role="status" className="flex items-center justify-center gap-2 bg-ink px-4 py-2 text-xs font-semibold text-cream">
      <WifiOff className="size-3.5" aria-hidden />
      You&apos;re offline. Reports you write are kept on this device until you reconnect.
    </div>
  );
}
