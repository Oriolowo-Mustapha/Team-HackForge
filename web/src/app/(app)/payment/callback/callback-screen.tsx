"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { BadgeCheck, RotateCcw, XCircle } from "lucide-react";
import { useSearchParams } from "next/navigation";
import { useEffect, useRef } from "react";
import { paymentsApi } from "@/lib/api/endpoints";
import { qk } from "@/lib/queries";
import { formatMoney } from "@/lib/format";
import { PaymentStatusBadge } from "@/components/domain/badges";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { EmptyState, ErrorState, Notice } from "@/components/ui/states";
import type { Payment } from "@/lib/api/schemas";

/**
 * Flutterwave lands here after hosted checkout with ?status=&tx_ref=. The server re-verifies the
 * reference with the provider — the URL params are only used to find the payment, never trusted.
 */
export function CallbackScreen() {
  const params = useSearchParams();
  const reference = params.get("tx_ref") ?? params.get("reference");
  const cancelled = params.get("status") === "cancelled";
  const client = useQueryClient();
  const started = useRef(false);

  const verify = useMutation({
    mutationFn: () => paymentsApi.verify(reference!),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: qk.payments({}) }),
        client.invalidateQueries({ queryKey: ["payment-quote"] }),
      ]);
    },
  });

  // Verify exactly once per pageload; retries are user-initiated.
  useEffect(() => {
    if (reference && !started.current) {
      started.current = true;
      void verify.mutate();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reference]);

  if (!reference) {
    return (
      <EmptyState icon={<XCircle />} title="Missing payment reference" action={<ButtonLink href="/payments" variant="secondary">Payment history</ButtonLink>}>
        We couldn&apos;t find a payment reference in the return URL. If you completed the payment, check the payment history in a minute.
      </EmptyState>
    );
  }

  if (verify.isPending) {
    return (
      <div className="mx-auto max-w-xl">
        <Card>
          <p className="font-semibold">Checking your payment…</p>
          <p className="mt-1 text-[15px] text-muted">Confirming with Flutterwave. This takes a few seconds — we&apos;ll take you to the case when it&apos;s done.</p>
        </Card>
      </div>
    );
  }

  if (verify.isError) {
    return (
      <div className="mx-auto max-w-xl">
        <ErrorState error={verify.error} onRetry={() => verify.reset()} className="mt-4" />
        <div className="mt-4 flex justify-center">
          <Button variant="secondary" icon={<RotateCcw className="size-4" />} onClick={() => verify.mutate()}>
            Check again
          </Button>
        </div>
      </div>
    );
  }

  const { payment, message } = verify.data!;

  if (payment.status !== "Completed") {
    return (
      <FailedView payment={payment} message={cancelled ? "The checkout was cancelled before payment." : message} />
    );
  }

  return (
    <div className="mx-auto max-w-xl space-y-5">
      <Notice tone="success" icon={<BadgeCheck />} title="Payment confirmed">
        {message || "The vet has been notified and can start on your case."}
      </Notice>
      <Card>
        <CardTitle>Receipt</CardTitle>
        <dl className="mt-3 space-y-2.5 text-[15px]">
          <div className="flex items-baseline justify-between gap-4">
            <dt className="text-muted">Total paid</dt>
            <dd className="font-semibold">{formatMoney(payment.totalAmount, payment.currency)}</dd>
          </div>
          <div className="flex items-baseline justify-between gap-4">
            <dt className="text-muted">Reference</dt>
            <dd className="truncate font-medium">{payment.transactionReference}</dd>
          </div>
          <div className="flex items-baseline justify-between gap-4">
            <dt className="text-muted">Status</dt>
            <dd>
              <PaymentStatusBadge status={payment.status} mode={payment.mode} />
            </dd>
          </div>
        </dl>
      </Card>
      <div className="flex flex-col gap-2 sm:flex-row">
        {payment.incidentId && (
          <ButtonLink href={`/cases/${payment.incidentId}`} variant="accent">
            Go to the case
          </ButtonLink>
        )}
        <ButtonLink href="/payments" variant="secondary">
          Payment history
        </ButtonLink>
      </div>
    </div>
  );
}

function FailedView({ payment, message }: { payment: Payment; message?: string }) {
  const caseHref = payment.incidentId ? `/cases/${payment.incidentId}` : "/cases";
  return (
    <div className="mx-auto max-w-xl space-y-5">
      <Notice tone="warning" icon={<XCircle />} title="Payment not completed">
        {message || "No charge went through, so nothing has been paid yet."}
      </Notice>
      <Card>
        <div className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="text-[15px] font-semibold">{formatMoney(payment.totalAmount, payment.currency)} due</p>
            <p className="mt-1 truncate text-[13px] text-muted">Ref {payment.transactionReference}</p>
          </div>
          <PaymentStatusBadge status={payment.status} mode={payment.mode} />
        </div>
      </Card>
      <div className="flex flex-col gap-2 sm:flex-row">
        {payment.vetRequestId && (
          <ButtonLink href={`/pay/${payment.vetRequestId}`} variant="accent">
            Try paying again
          </ButtonLink>
        )}
        <ButtonLink href={caseHref} variant="secondary">
          Back to the case
        </ButtonLink>
      </div>
      <p className="text-center text-[13px] text-faint">If you were charged but the status stays unpaid, contact support with the reference above.</p>
    </div>
  );
}
