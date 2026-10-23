"use client";

import { useInfiniteQuery } from "@tanstack/react-query";
import { ClipboardList, Search } from "lucide-react";
import { useDeferredValue, useState } from "react";
import { incidentsApi, type IncidentFilters } from "@/lib/api/endpoints";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { useSession } from "@/lib/hooks/use-session";
import { qk } from "@/lib/queries";
import { IncidentRow } from "@/components/domain/rows";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Segmented } from "@/components/ui/segmented";
import { ListSkeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/states";

type View = "open" | "resolved" | "all";

export function CasesScreen() {
  const { user } = useSession();
  const { farm } = useActiveFarm();
  const isFarmer = user?.role === "Farmer";
  const [view, setView] = useState<View>("open");
  const [search, setSearch] = useState("");
  const q = useDeferredValue(search.trim());

  const filters: IncidentFilters = {
    farmId: isFarmer ? farm?.id : undefined,
    openOnly: view === "open" ? true : undefined,
    status: view === "resolved" ? 4 : undefined,
    q: q || undefined,
    pageSize: 20,
  };

  const list = useInfiniteQuery({
    queryKey: qk.incidents(filters),
    queryFn: ({ pageParam }) => incidentsApi.search({ ...filters, page: pageParam }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
    enabled: !isFarmer || Boolean(farm),
    placeholderData: (prev) => prev,
  });

  const items = list.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title={isFarmer ? "Cases" : "My cases"}
        subtitle={
          isFarmer
            ? "Concerns that need tracking — actions, follow-ups and vet visits in one place."
            : "Cases where you accepted the farmer's request. Contact details are visible only for these."
        }
      />
      <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Segmented
          label="Filter cases"
          value={view}
          onChange={setView}
          options={[
            { value: "open", label: "Open" },
            { value: "resolved", label: "Resolved" },
            { value: "all", label: "All" },
          ]}
        />
        <div className="relative sm:w-64">
          <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-faint" aria-hidden />
          <Input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search cases…" className="pl-11" aria-label="Search cases" />
        </div>
      </div>
      {list.isPending ? (
        <ListSkeleton rows={4} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState icon={<ClipboardList />} title={q ? "No matching cases" : view === "open" ? "No open cases" : "No cases here"}>
          {isFarmer
            ? "A case opens automatically when a confirmed report looks high or critical — or when you choose to track one."
            : "Accept a request from the Requests tab and the case appears here."}
        </EmptyState>
      ) : (
        <div className="space-y-2.5">
          {items.map((item) => (
            <IncidentRow key={item.incident.id} item={item} showFarm={!isFarmer} />
          ))}
          {list.hasNextPage && (
            <Button variant="secondary" block loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()}>
              Load more
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
