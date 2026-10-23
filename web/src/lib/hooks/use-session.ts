"use client";

import { useQuery } from "@tanstack/react-query";
import { sessionApi, systemApi } from "@/lib/api/endpoints";
import type { SessionUser } from "@/lib/api/schemas";
import { qk } from "@/lib/queries";

export function useSession() {
  const query = useQuery({ queryKey: qk.session, queryFn: () => sessionApi.get(), staleTime: 60_000, retry: 1 });
  const data = query.data;
  const user: SessionUser | null = data && data.authenticated ? data.user : null;
  return {
    user,
    isLoading: query.isPending,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
    status: query.isPending ? "loading" : user ? "authenticated" : "anonymous",
  } as const;
}

export function useSystemInfo() {
  return useQuery({ queryKey: qk.system, queryFn: () => systemApi.info(), staleTime: 5 * 60_000 });
}
