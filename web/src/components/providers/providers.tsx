"use client";

import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";
import { Toaster } from "sonner";
import { ApiError, onSessionExpired } from "@/lib/api/client";
import { qk } from "@/lib/queries";
import { clearLocalData } from "@/lib/drafts";
import { MotionConfig } from "motion/react";

function shouldRetry(failureCount: number, error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 0 || error.status === 502 || error.status === 503) return failureCount < 2;
    return false;
  }
  return failureCount < 1;
}

export function Providers({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { staleTime: 20_000, retry: shouldRetry, refetchOnWindowFocus: true },
          mutations: { retry: false },
        },
        queryCache: new QueryCache(),
        mutationCache: new MutationCache(),
      }),
  );

  useEffect(
    () =>
      onSessionExpired(() => {
        void clearLocalData();
        client.clear();
        client.setQueryData(qk.session, { authenticated: false, expired: true });
        const next = encodeURIComponent(window.location.pathname + window.location.search);
        // A full page load guarantees no signed-in client state survives an expired session.
        // eslint-disable-next-line @next/next/no-location-assign-relative-destination
        if (!window.location.pathname.startsWith("/login")) window.location.assign(`/login?expired=1&next=${next}`);
      }),
    [client],
  );

  return (
    <QueryClientProvider client={client}>
      <MotionConfig reducedMotion="user">{children}</MotionConfig>
      <Toaster
        position="top-center"
        toastOptions={{
          classNames: {
            toast: "!rounded-2xl !border !border-line !bg-paper !text-ink !shadow-lift !font-sans",
            description: "!text-muted",
          },
        }}
      />
    </QueryClientProvider>
  );
}
