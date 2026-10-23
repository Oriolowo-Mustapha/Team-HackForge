"use client";

import { useCallback, useSyncExternalStore } from "react";
import { useSession } from "./use-session";

const KEY = "fh:active-farm";
const listeners = new Set<() => void>();

function read(): string | null {
  try {
    return localStorage.getItem(KEY);
  } catch {
    return null;
  }
}

/** The farm the farmer is working on (remembered per device), defaulting to their first farm. */
export function useActiveFarm() {
  const { user, isLoading } = useSession();
  const stored = useSyncExternalStore(
    (cb) => {
      listeners.add(cb);
      return () => listeners.delete(cb);
    },
    read,
    () => null,
  );

  const farms = user?.farms ?? [];
  const farm = farms.find((f) => f.id === stored) ?? farms[0] ?? null;

  const setFarm = useCallback((id: string) => {
    try {
      localStorage.setItem(KEY, id);
    } catch {
      // Storage may be blocked; the default farm is used instead.
    }
    listeners.forEach((l) => l());
  }, []);

  return { farm, farms, setFarm, isLoading };
}
