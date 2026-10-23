"use client";

import { useEffect } from "react";
import { toast } from "sonner";

/** Registers the service worker in production builds and offers a reload when an update is ready. */
export function ServiceWorkerRegistrar() {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production" || !("serviceWorker" in navigator)) return;

    let cancelled = false;
    navigator.serviceWorker
      .register("/sw.js", { scope: "/" })
      .then((registration) => {
        registration.addEventListener("updatefound", () => {
          const worker = registration.installing;
          worker?.addEventListener("statechange", () => {
            if (cancelled || worker.state !== "installed" || !navigator.serviceWorker.controller) return;
            toast("A new version of FarmHand is ready", {
              action: { label: "Reload", onClick: () => worker.postMessage({ type: "SKIP_WAITING" }) },
              duration: Infinity,
            });
          });
        });
      })
      .catch(() => {
        // Without a service worker the app still works online; offline support is simply unavailable.
      });

    // Only reload when an existing worker is replaced, not when the first one takes control.
    const hadController = Boolean(navigator.serviceWorker.controller);
    let reloading = false;
    const onControllerChange = () => {
      if (reloading || !hadController) return;
      reloading = true;
      window.location.reload();
    };
    navigator.serviceWorker.addEventListener("controllerchange", onControllerChange);
    return () => {
      cancelled = true;
      navigator.serviceWorker.removeEventListener("controllerchange", onControllerChange);
    };
  }, []);

  return null;
}
