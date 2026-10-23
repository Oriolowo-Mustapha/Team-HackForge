import type { Metadata } from "next";
import { WifiOff } from "lucide-react";
import { BrandMark } from "@/components/shell/brand";
import { RetryButton } from "./retry-button";

export const metadata: Metadata = { title: "Offline" };

/** Served by the service worker when a page can't be loaded without a connection. */
export default function OfflinePage() {
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center bg-cream px-6 text-center">
      <BrandMark className="size-14" />
      <div className="mt-8 flex size-14 items-center justify-center rounded-2xl bg-paper shadow-soft">
        <WifiOff className="size-6" aria-hidden />
      </div>
      <h1 className="mt-5 text-[28px] font-semibold tracking-[-0.03em]">You&apos;re offline</h1>
      <p className="mt-2 max-w-sm text-[15px] leading-relaxed text-muted">
        This page needs a connection. Health reports you started are saved on this device and can be sent from <strong className="text-ink">Report a concern</strong> once you&apos;re back online.
      </p>
      <RetryButton />
    </main>
  );
}
