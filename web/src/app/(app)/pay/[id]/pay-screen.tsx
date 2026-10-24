"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BadgeCheck, FileWarning, Lock, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { paymentsApi } from "@/lib/api/endpoints";
import { qk } from "@/lib/queries";
import { formatMoney } from "@/lib/format";
import { PaymentStatusBadge } from "@/components/domain/badges";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorState, InlineError, Notice } from "@/components/ui/states";
import type { Payment } from "@/lib/api/schemas";

function idempotencyKey(): string {
  return typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
}

export function PayScreen({ vetRequestId }: { vetRequestId: string }) {
  const client = useQueryClient();
  const [paid, setPaid] = useState<Payment | null>(null);
  const quote = useQuery({ queryKey: qk.paymentQuote(vetRequestId), queryFn: () => paymentsApi.quote(vetRequestId) });

  // Open a checkout (live) or finish immediately (simulated mode has no hosted checkout page).
  const initiate = useMutation({
    mutationFn: () => paymentsApi.initiate(vetRequestId, quote.data!.vetFee, quote.data!.currency, idempotencyKey()),
    onSuccess: async (created) => {
      if (created.checkoutUrl) {
        window.location.assign(created.checkoutUrl);
        return;
      }
      // Simulated mode: there is no hosted checkout, so verify the signed reference straight away.
      const verification = await paymentsApi.verify(created.transactionReference);
      toast.success(verification.message || "Payment confirmed.");
      await Promise.all([
        client.invalidateQueries({ queryKey: qk.paymentQuote(vetRequestId) }),
        client.invalidateQueries({ queryKey: qk.payments({}) }),
      ]);
      setPaid(verification.payment);
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
        subtitle={q.vetName ? `${q.vetName} is paid once checkout completes.` : "Your vet is paid once checkout completes."}
      />

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
        <InlineError error={initiate.error} className="mt-3" />
        <div className="mt-4">
          <Button variant="accent" block size="lg" loading={initiate.isPending} onClick={() => initiate.mutate()}>
            Pay {formatMoney(q.total, q.currency)}
          </Button>
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
      <Notice tone="success" icon={<BadgeCheck />} title="Payment complete">
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
