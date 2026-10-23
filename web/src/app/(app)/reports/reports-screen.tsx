"use client";

import { useInfiniteQuery } from "@tanstack/react-query";
import { ClipboardList, HeartPulse } from "lucide-react";
import { useState } from "react";
import { observationsApi } from "@/lib/api/endpoints";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { qk } from "@/lib/queries";
import { ObservationRow } from "@/components/domain/rows";
import { Button, ButtonLink } from "@/components/ui/button";
import { PageHeader } from "@/components/ui/page-header";
import { Segmented } from "@/components/ui/segmented";
import { ListSkeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/states";

export function ReportsScreen({ initialStatus }: { initialStatus: "all" | "pending" }) {
  const { farm } = useActiveFarm();
  const [status, setStatus] = useState(initialStatus);
  const filters = { farmId: farm?.id, status: status === "pending" ? 0 : undefined, pageSize: 20 };

  const list = useInfiniteQuery({
    queryKey: qk.observations(filters),
    queryFn: ({ pageParam }) => observationsApi.list({ ...filters, page: pageParam }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
    enabled: Boolean(farm),
  });

  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const total = list.data?.pages[0]?.totalCount ?? 0;

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title="Health reports"
        subtitle="Every check you send — by voice, photo or text. Reports waiting for review aren't part of any case until you confirm them."
        actions={
          <ButtonLink href="/scan" variant="accent" icon={<HeartPulse className="size-4" />} className="hidden sm:inline-flex">
            New report
          </ButtonLink>
        }
      />
      <Segmented
        label="Filter reports"
        className="mb-5"
        value={status}
        onChange={setStatus}
        options={[
          { value: "all", label: "All reports", count: status === "all" ? total : undefined },
          { value: "pending", label: "Needs review", count: status === "pending" ? total : undefined },
        ]}
      />
      {list.isPending ? (
        <ListSkeleton rows={5} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState icon={<ClipboardList />} title={status === "pending" ? "Nothing waiting for review" : "No reports yet"} action={<ButtonLink href="/scan" variant="accent">Report a concern</ButtonLink>}>
          {status === "pending" ? "You've reviewed everything you sent." : "Your first report takes less than a minute."}
        </EmptyState>
      ) : (
        <div className="space-y-2.5">
          {items.map((o) => (
            <ObservationRow key={o.id} item={o} />
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
