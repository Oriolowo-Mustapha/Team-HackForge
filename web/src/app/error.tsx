"use client";

import { Button } from "@/components/ui/button";

export default function ErrorPage({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-cream p-6 text-center">
      <h1 className="text-2xl font-semibold">This page could not be loaded</h1>
      <p>
        Please try again. If the problem continues, contact your FarmHand
        administrator.
      </p>
      <Button onClick={reset}>Try again</Button>
    </main>
  );
}
