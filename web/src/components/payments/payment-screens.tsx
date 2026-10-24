"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { paymentsApi } from "@/lib/api/endpoints";
import type { Payment } from "@/lib/api/schemas";
import { formatDateTime, formatMoney } from "@/lib/format";
import { useSession } from "@/lib/hooks/use-session";
import { qk } from "@/lib/queries";
import { PaymentStatusBadge } from "@/components/domain/badges";
import { Button, ButtonLink } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Select } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Pager, Panel, ResourcePanel } from "@/components/ui/resource-panel";
import { EmptyState, InlineError, Notice } from "@/components/ui/states";

function checkoutUrl(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    return url.protocol === "https:" ? url.href : null;
  } catch {
    return null;
  }
}

export function PaymentCard({ payment }: { payment: Payment }) {
  const { user } = useSession();
  const client = useQueryClient();
  const [confirm, setConfirm] = useState(false);
  const refresh = () =>
    Promise.all([
      client.invalidateQueries({ queryKey: ["payments"] }),
      client.invalidateQueries({ queryKey: ["admin", "payments"] }),
      client.invalidateQueries({ queryKey: ["payment-quote"] }),
      client.invalidateQueries({ queryKey: ["incident-case"] }),
    ]);
  const verify = useMutation({
    mutationFn: () => paymentsApi.verify(payment.transactionReference),
    onSuccess: refresh,
  });
  const cancel = useMutation({
    mutationFn: () => paymentsApi.cancel(payment.transactionReference),
    onSuccess: async () => {
      setConfirm(false);
      await refresh();
    },
  });
  const canManage = user?.role === "Farmer" || user?.role === "Admin";
  return (
    <Panel>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">
            {formatMoney(payment.totalAmount, payment.currency)}
          </h2>
          <p className="text-sm text-muted">
            {payment.farmName ?? "Vet consultation"} ·{" "}
            {formatDateTime(payment.createdAt)}
          </p>
        </div>
        <PaymentStatusBadge status={payment.status} mode={payment.mode} />
      </div>
      <dl className="my-4 grid gap-2 text-sm sm:grid-cols-3">
        <div>
          <dt className="text-muted">Vet fee</dt>
          <dd>{formatMoney(payment.amount, payment.currency)}</dd>
        </div>
        <div>
          <dt className="text-muted">Platform fee</dt>
          <dd>{formatMoney(payment.platformCommission, payment.currency)}</dd>
        </div>
        <div>
          <dt className="text-muted">Reference</dt>
          <dd className="break-all text-xs">{payment.transactionReference}</dd>
        </div>
      </dl>
      {payment.failureReason && (
        <p className="mb-3 text-sm text-red-ink">{payment.failureReason}</p>
      )}
      <div className="flex flex-wrap gap-2">
        {payment.incidentId && (
          <ButtonLink
            href={`/cases/${payment.incidentId}`}
            size="sm"
            variant="secondary"
          >
            View case
          </ButtonLink>
        )}
        {payment.status === "Pending" && canManage && (
          <>
            {user?.role === "Farmer" && (
              <ButtonLink href={`/pay/${payment.vetRequestId}`} size="sm">
                Continue checkout
              </ButtonLink>
            )}
            <Button
              variant="secondary"
              size="sm"
              loading={verify.isPending}
              onClick={() => verify.mutate()}
            >
              Check payment status
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setConfirm(true)}>
              Cancel checkout
            </Button>
          </>
        )}
      </div>
      {verify.data && (
        <p role="status" className="mt-3 text-sm">
          {verify.data.message}
        </p>
      )}
      <InlineError error={verify.error ?? cancel.error} className="mt-3" />
      <ConfirmDialog
        open={confirm}
        onOpenChange={setConfirm}
        title="Cancel this checkout?"
        description="You can start a new checkout later. A completed payment cannot be cancelled here."
        confirmLabel="Cancel checkout"
        onConfirm={() => cancel.mutate()}
        loading={cancel.isPending}
      />
    </Panel>
  );
}

export function PaymentsScreen() {
  const { user } = useSession();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const filters = { status: status || undefined, page, pageSize: 20 };
  const query = useQuery({
    queryKey: qk.payments(filters),
    queryFn: () => paymentsApi.list(filters),
  });
  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title={user?.role === "Vet" ? "Earnings" : "Payments"}
        subtitle="Verified consultation payments, including clearly labelled demo transactions."
      />
      <Select
        aria-label="Payment status"
        value={status}
        onChange={(e) => {
          setStatus(e.target.value);
          setPage(1);
        }}
        className="mb-5"
      >
        <option value="">All statuses</option>
        {["Pending", "Completed", "Failed", "Cancelled"].map((s) => (
          <option key={s}>{s}</option>
        ))}
      </Select>
      <ResourcePanel query={query}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="space-y-3">
                {data.items.map((payment) => (
                  <PaymentCard payment={payment} key={payment.id} />
                ))}
              </div>
            ) : (
              <EmptyState title="No payments yet">
                Consultation payments will appear here.
              </EmptyState>
            )}
            <Pager
              page={page}
              totalPages={data.totalPages}
              onChange={setPage}
            />
          </>
        )}
      </ResourcePanel>
    </div>
  );
}

export function PayScreen({ requestId }: { requestId: string }) {
  const client = useQueryClient();
  const [idempotencyKey] = useState(() => crypto.randomUUID());
  const quote = useQuery({
    queryKey: qk.paymentQuote(requestId),
    queryFn: () => paymentsApi.quote(requestId),
  });
  const initiate = useMutation({
    mutationFn: () => {
      if (!quote.data) throw new Error("Load the fee first.");
      return paymentsApi.initiate(
        requestId,
        quote.data.vetFee,
        quote.data.currency,
        idempotencyKey,
      );
    },
    onSuccess: async (result) => {
      await client.invalidateQueries({ queryKey: qk.paymentQuote(requestId) });
      const url = checkoutUrl(result.checkoutUrl);
      if (result.mode === "Live" && url) window.location.assign(url);
    },
  });
  const verify = useMutation({
    mutationFn: () => {
      const reference =
        initiate.data?.transactionReference ??
        quote.data?.pendingPayment?.transactionReference;
      if (!reference) throw new Error("Start checkout first.");
      return paymentsApi.verify(reference);
    },
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["payments"] });
      await quote.refetch();
    },
  });
  const pending = quote.data?.pendingPayment;
  const mode = initiate.data?.mode ?? quote.data?.mode;
  const reference =
    initiate.data?.transactionReference ?? pending?.transactionReference;
  const hosted = checkoutUrl(
    initiate.data?.checkoutUrl ?? pending?.checkoutUrl,
  );
  return (
    <div className="mx-auto max-w-xl">
      <PageHeader
        title="Review and pay"
        back="/cases"
        subtitle="The total is checked by the server before checkout."
      />
      <ResourcePanel query={quote}>
        {(data) => (
          <div className="space-y-4">
            <Panel title={data.vetName ?? "Vet consultation"}>
              <dl className="space-y-3 text-sm">
                {[
                  ["Vet fee", data.vetFee],
                  ["Platform fee", data.platformCommission],
                  ["Total", data.total],
                ].map(([label, amount]) => (
                  <div
                    key={String(label)}
                    className="flex justify-between gap-3"
                  >
                    <dt>{label}</dt>
                    <dd className="font-semibold">
                      {formatMoney(Number(amount), data.currency)}
                    </dd>
                  </div>
                ))}
              </dl>
            </Panel>
            {mode === "Simulated" && (
              <Notice tone="demo" title="Simulated payment">
                This is a demonstration. No money will be charged or
                transferred.
              </Notice>
            )}
            {data.reason && <Notice>{data.reason}</Notice>}
            {!reference && (
              <Button
                block
                loading={initiate.isPending}
                disabled={!data.canPay}
                onClick={() => initiate.mutate()}
              >
                {mode === "Simulated"
                  ? "Start simulated checkout"
                  : "Continue to secure checkout"}
              </Button>
            )}
            {reference && (
              <Panel title="Checkout status">
                <p className="mb-4 break-all text-xs text-muted">{reference}</p>
                <div className="flex flex-wrap gap-2">
                  {hosted && mode === "Live" && (
                    <a
                      className="rounded-2xl bg-ink px-5 py-3 font-semibold text-cream"
                      href={hosted}
                    >
                      Open secure checkout
                    </a>
                  )}
                  <Button
                    loading={verify.isPending}
                    onClick={() => verify.mutate()}
                  >
                    {mode === "Simulated"
                      ? "Confirm simulated payment"
                      : "Verify payment"}
                  </Button>
                </div>
              </Panel>
            )}
            {verify.data && (
              <Notice
                tone={
                  verify.data.payment.status === "Completed"
                    ? "success"
                    : "info"
                }
                title={verify.data.payment.status}
              >
                {verify.data.message}
              </Notice>
            )}
            <InlineError error={initiate.error ?? verify.error} />
            <ButtonLink href="/payments" variant="secondary" block>
              Payment history
            </ButtonLink>
          </div>
        )}
      </ResourcePanel>
    </div>
  );
}

export function PaymentReturnScreen({
  reference,
}: {
  reference: string | null;
}) {
  const verify = useMutation({
    mutationFn: () => {
      if (!reference) throw new Error("No payment reference was provided.");
      return paymentsApi.verify(reference);
    },
  });
  return (
    <div className="mx-auto max-w-xl">
      <PageHeader
        title="Verify your payment"
        subtitle="The checkout return alone does not confirm a charge. Check the provider's verified result below."
      />
      <Panel>
        <p className="mb-4 break-all text-sm">
          {reference ??
            "The return link is missing a payment reference. Open payment history to continue."}
        </p>
        {reference && (
          <Button onClick={() => verify.mutate()} loading={verify.isPending}>
            Verify payment
          </Button>
        )}
        <InlineError error={verify.error} className="mt-4" />
        {verify.data && (
          <div role="status" className="mt-4 space-y-3">
            <PaymentStatusBadge
              status={verify.data.payment.status}
              mode={verify.data.payment.mode}
            />
            <p>{verify.data.message}</p>
          </div>
        )}
        <ButtonLink href="/payments" variant="secondary" className="mt-5">
          Payment history
        </ButtonLink>
      </Panel>
    </div>
  );
}
