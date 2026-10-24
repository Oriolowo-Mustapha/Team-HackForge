"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BadgeCheck, FileWarning, Lock, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { paymentsApi } from "@/lib/api/endpoints";
import { newIdempotencyKey } from "@/lib/api/client";
import { qk } from "@/lib/queries";
import { formatMoney } from "@/lib/format";
import { PaymentStatusBadge } from "@/components/domain/badges";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorState, InlineError, Notice } from "@/components/ui/states";
import type { Payment } from "@/lib/api/schemas";

export function PayScreen({ vetRequestId }: { vetRequestId: string }) {
  const client = useQueryClient();
  const [paid, setPaid] = useState<Payment | null>(null);
  const [idempotencyKey] = useState(() => newIdempotencyKey("payment"));
  const quote = useQuery({ queryKey: qk.paymentQuote(vetRequestId), queryFn: () => paymentsApi.quote(vetRequestId) });

  const refreshPayments = () => Promise.all([
    client.invalidateQueries({ queryKey: ["payment-quote"] }),
    client.invalidateQueries({ queryKey: ["payments"] }),
    client.invalidateQueries({ queryKey: ["incident-case"] }),
    client.invalidateQueries({ queryKey: ["vet-request"] }),
  ]);

  // Resume an existing reference; a retry keeps the same key if initiation was interrupted.
  const initiate = useMutation({
    mutationFn: async () => {
      if (!quote.data) throw new Error("Load the consultation fee first.");
      const created = quote.data.pendingPayment ?? await paymentsApi.initiate(vetRequestId, quote.data.vetFee, quote.data.currency, idempotencyKey);
      if (created.mode === "Live") {
        if (!created.checkoutUrl || new URL(created.checkoutUrl).protocol !== "https:") throw new Error("A secure checkout link is not available. Verify the existing payment or try again.");
        window.location.assign(created.checkoutUrl);
        return null;
      }
      return paymentsApi.verify(created.transactionReference);
    },
    onSuccess: async (verification) => {
      if (!verification) return;
      toast.success(verification.message || "Payment confirmed.");
      setPaid(verification.payment);
      await refreshPayments();
    },
    onError: () => { void quote.refetch(); },
  });
  const verify = useMutation({
    mutationFn: () => {
      if (!quote.data?.pendingPayment) throw new Error("No pending payment to verify.");
      return paymentsApi.verify(quote.data.pendingPayment.transactionReference);
    },
    onSuccess: async (verification) => {
      setPaid(verification.payment);
      await refreshPayments();
    },
  });

  if (quote.isPending) {
    return (
      <div className="mx-auto max-w-xl">
        <Skeleton className="h-64 rounded-[28px]" />
      </div>
    );
  }
  if (quote.isError) return <ErrorState error={quote.error} onRetry={() => void quote.refetch()} />;

  const q = quote.data;
  if (paid) return <PaidView payment={paid} />;

  if (!q.canPay) {
    return (
      <div className="mx-auto max-w-xl">
        <PageHeader back="/home" title="Review and pay" />
        <Card>
          <div className="flex items-start gap-3">
            <FileWarning className="mt-0.5 size-5 shrink-0 text-terracotta-deep" aria-hidden />
            <div>
              <p className="font-semibold">Payment isn&apos;t open</p>
              <p className="mt-1 text-[15px] leading-relaxed text-muted">{q.reason ?? "This request cannot be paid right now."}</p>
            </div>
          </div>
          <div className="mt-4 flex flex-col gap-2 sm:flex-row">
            <ButtonLink href="/cases" variant="secondary">
              Back to cases
            </ButtonLink>
            <ButtonLink href="/payments" variant="ghost">
              Payment history
            </ButtonLink>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-xl space-y-5">
      <PageHeader
        back="/home"
        title="Review and pay"
        subtitle="The server verifies the total and payment status before the consultation is marked paid."
      />

      {q.mode === "Simulated" && <Notice tone="demo" title="Simulated payment">This is a demonstration. No money will be charged or transferred.</Notice>}

      {q.pendingPayment && (
        <Notice tone="info" icon={<BadgeCheck />} title="A checkout is already open">
          Continue with it below — FarmHand resumes the same payment instead of charging you twice.
        </Notice>
      )}

      <Card>
        <CardTitle>Payment summary</CardTitle>
        <dl className="mt-3 space-y-2.5 text-[15px]">
          <FeeRow label="Veterinarian" value={q.vetName ?? "Assigned after acceptance"} />
          <FeeRow label="Consultation fee" value={formatMoney(q.vetFee, q.currency)} />
          <FeeRow label={`FarmHand service (${q.commissionPercentage}%)`} value={formatMoney(q.platformCommission, q.currency)} />
          <div className="border-t border-line pt-2.5">
            <FeeRow label="Total" value={formatMoney(q.total, q.currency)} strong />
          </div>
        </dl>
        <p className="mt-3 flex items-center gap-1.5 text-[13px] text-muted">
          <Lock className="size-3.5" aria-hidden /> Paid through Flutterwave&apos;s secure checkout.
          {q.mode === "Simulated" && " Simulation mode: no real money moves."}
        </p>
        <InlineError error={initiate.error ?? verify.error} className="mt-3" />
        <div className="mt-4">
          <Button variant="accent" block size="lg" loading={initiate.isPending} disabled={verify.isPending} onClick={() => initiate.mutate()}>
            {q.mode === "Simulated" ? "Confirm simulated payment" : q.pendingPayment ? "Resume secure checkout" : `Pay ${formatMoney(q.total, q.currency)}`}
          </Button>
          {q.pendingPayment && q.mode === "Live" && <Button variant="secondary" block className="mt-2" loading={verify.isPending} disabled={initiate.isPending} onClick={() => verify.mutate()}>Verify existing payment</Button>}
        </div>
      </Card>

      <Card>
        <div className="flex items-center gap-3">
          <ShieldCheck className="size-5 shrink-0 text-green-ink" aria-hidden />
          <p className="text-[14.5px] leading-relaxed text-muted">
            You pay once, in full. The vet receives the consultation fee and FarmHand keeps the 5% service. A payment only counts once the provider confirms it.
          </p>
        </div>
      </Card>
    </div>
  );
}

function FeeRow({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className={strong ? "font-semibold" : "text-muted"}>{label}</dt>
      <dd className={strong ? "text-[17px] font-semibold" : "font-medium"}>{value}</dd>
    </div>
  );
}

/** Success view shown after a simulated checkout — live checkouts land on /payment/callback instead. */
function PaidView({ payment }: { payment: Payment }) {
  const caseHref = payment.incidentId ? `/cases/${payment.incidentId}` : "/payments";
  return (
    <div className="mx-auto max-w-xl space-y-5">
      <Notice tone={payment.status === "Completed" ? "success" : "info"} icon={<BadgeCheck />} title={payment.status === "Completed" ? "Payment complete" : "Payment status"}>
        {payment.status === "Completed"
          ? "The vet has been notified and can start on your case."
          : `${payment.status} — ${payment.failureReason ?? "check the payment history for details."}`}
      </Notice>
      <Card>
        <div className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="text-[15px] font-semibold">{formatMoney(payment.totalAmount, payment.currency)}</p>
            <p className="mt-1 truncate text-[13px] text-muted">Ref {payment.transactionReference}</p>
          </div>
          <PaymentStatusBadge status={payment.status} mode={payment.mode} />
        </div>
      </Card>
      <ButtonLink href={caseHref} variant="accent" block>
        {payment.incidentId ? "Go to the case" : "See payment history"}
      </ButtonLink>
    </div>
  );
}
