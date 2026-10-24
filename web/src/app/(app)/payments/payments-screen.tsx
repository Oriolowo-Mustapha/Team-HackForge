"use client";

import { useInfiniteQuery } from "@tanstack/react-query";
import { CheckCircle2, ChevronRight, Wallet } from "lucide-react";
import Link from "next/link";
import { paymentsApi } from "@/lib/api/endpoints";
import { qk } from "@/lib/queries";
import { formatMoney, formatDateTime } from "@/lib/format";
import { PaymentStatusBadge } from "@/components/domain/badges";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/ui/page-header";
import { ListSkeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/states";
import type { Payment } from "@/lib/api/schemas";
import { cn } from "@/lib/cn";

export function PaymentsScreen() {
  const list = useInfiniteQuery({
    queryKey: qk.payments({}),
    queryFn: ({ pageParam }) => paymentsApi.list({ page: pageParam, pageSize: 20 }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });

  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const total = list.data?.pages[0]?.totalCount ?? 0;

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title="Payments"
        subtitle="Consultation payments for veterinary visits. FarmHand's 5% service fee is included in every total."
      />
      {list.isPending ? (
        <ListSkeleton rows={5} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState icon={<Wallet />} title="No payments yet">
          {total === 0
            ? "When you pay for an accepted vet visit, it shows up here."
            : `${total} payment${total === 1 ? "" : "s"} on record.`}
        </EmptyState>
      ) : (
        <>
          <div className="space-y-2.5">
            {items.map((p) => (
              <PaymentRow key={p.id} payment={p} />
            ))}
          </div>
          {list.hasNextPage && (
            <div className="mt-4">
              <Button variant="secondary" block loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()}>
                Load more
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function PaymentRow({ payment }: { payment: Payment }) {
  const paid = payment.status === "Completed";
  const inner = (
    <>
      <span
        className={cn(
          "flex size-14 shrink-0 items-center justify-center rounded-[18px] ring-1 ring-line/60",
          paid ? "bg-green-soft text-green-ink" : "bg-sand text-muted",
        )}
      >
        {paid ? <CheckCircle2 className="size-6" aria-hidden /> : <Wallet className="size-6" aria-hidden />}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-semibold">{formatMoney(payment.totalAmount, payment.currency)}</p>
        <p className="truncate text-[13px] text-muted">
          {payment.incidentId ? "Veterinary case" : "Vet consultation"} · {payment.mode === "Simulated" ? "Simulated" : "Card"} ·{" "}
          {payment.paidAt ? `Paid ${formatDateTime(payment.paidAt)}` : `Created ${formatDateTime(payment.createdAt)}`}
        </p>
        <div className="mt-2 flex flex-wrap gap-1.5">
          <PaymentStatusBadge status={payment.status} mode={payment.mode} />
        </div>
      </div>
    </>
  );

  if (payment.incidentId) {
    return (
      <Link href={`/cases/${payment.incidentId}`} className="group flex items-center gap-3.5 rounded-[24px] bg-paper p-3 pr-4 ring-1 ring-line/60 transition-colors hover:bg-white">
        {inner}
        <ChevronRight className="size-4 shrink-0 text-faint transition-transform group-hover:translate-x-0.5" aria-hidden />
      </Link>
    );
  }
  return <div className="flex items-center gap-3.5 rounded-[24px] bg-paper p-3 pr-4 ring-1 ring-line/60">{inner}</div>;
}
