"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CalendarClock,
  Check,
  CheckCircle2,
  ClipboardCheck,
  FileText,
  MapPin,
  Phone,
  Plus,
  ShieldPlus,
  Stethoscope,
  Users,
  Wallet,
  X,
} from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { incidentsApi, vetsApi } from "@/lib/api/endpoints";
import { IncidentStatus, Outcome, incidentStatusMeta, outcomeMeta, requestTypeMeta } from "@/lib/api/enums";
import type { IncidentCase, TreatmentPlan, VetRequest } from "@/lib/api/schemas";
import { cn } from "@/lib/cn";
import { formatDate, formatDateTime, formatMoney, formatRelative } from "@/lib/format";
import { useSession } from "@/lib/hooks/use-session";
import { smallPhoto, speciesPhoto } from "@/lib/images";
import { qk } from "@/lib/queries";
import { AssessmentCard } from "@/components/domain/assessment-card";
import { IncidentStatusBadge, PaymentStatusBadge, RequestStatusBadge, SeverityBadge, SourceBadge } from "@/components/domain/badges";
import { Badge } from "@/components/ui/badge";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { ConfirmDialog, Modal } from "@/components/ui/dialog";
import { ChoiceChips, Field, Input, Select, Textarea } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorState, InlineError, Notice } from "@/components/ui/states";
import { RequestVetModal } from "./request-vet-modal";
import { TreatmentPlanModal } from "./treatment-plan-modal";
import { ListenButton } from "@/components/domain/listen-button";

export function CaseFile({ id, openRequest }: { id: string; openRequest?: boolean }) {
  const { user } = useSession();
  const query = useQuery({ queryKey: qk.incidentCase(id), queryFn: () => incidentsApi.caseFile(id) });
  const [requesting, setRequesting] = useState(Boolean(openRequest));

  if (query.isPending)
    return (
      <div className="space-y-4">
        <Skeleton className="h-56 rounded-[32px]" />
        <div className="grid gap-4 lg:grid-cols-2">
          <Skeleton className="h-64 rounded-[28px]" />
          <Skeleton className="h-64 rounded-[28px]" />
        </div>
      </div>
    );
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;

  const data = query.data;
  const { incident } = data;
  const canWrite = data.access === "Owner" || data.access === "Admin" || data.access === "AssignedVet";
  const closed = incident.status === IncidentStatus.Resolved;
  const back = user?.role === "Admin" ? "/admin/cases" : "/cases";

  return (
    <div>
      <PageHeader back={back} eyebrow={`${data.farmName} · opened ${formatRelative(incident.detectedAt)}`} title={incident.incidentType} />

      <div className="grid gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-7">
          <Summary data={data} />
          <VetSection data={data} onRequest={() => setRequesting(true)} />
          {data.treatmentPlans.length > 0 && <TreatmentPlans plans={data.treatmentPlans} incidentId={incident.id} canWrite={canWrite} />}
          <ActionsSection data={data} canWrite={canWrite && !closed} />
          <FollowUpsSection data={data} canWrite={canWrite && !closed} />
        </div>
        <aside className="space-y-6 lg:col-span-5">
          {canWrite && data.allowedTransitions.length > 0 && <StatusControl data={data} />}
          {data.sourceObservation?.assessment && (
            <div>
              <AssessmentCard assessment={data.sourceObservation.assessment} />
              <Link href={`/reports/${data.sourceObservation.id}`} className="mt-2 inline-flex items-center gap-1.5 px-2 text-sm font-semibold text-muted hover:text-ink">
                <FileText className="size-4" aria-hidden /> View the original report
              </Link>
            </div>
          )}
          <PreventionSection data={data} canWrite={canWrite} />
          <Timeline data={data} />
        </aside>
      </div>

      {data.canRequestVet && <RequestVetModal open={requesting} onOpenChange={setRequesting} data={data} />}
    </div>
  );
}

function Summary({ data }: { data: IncidentCase }) {
  const { incident } = data;
  return (
    <section className="overflow-hidden rounded-[32px] bg-paper ring-1 ring-line/60">
      <div className="flex gap-4 p-5">
        <span className="relative block size-20 shrink-0 overflow-hidden rounded-[22px] bg-sand">
          <Image src={smallPhoto(speciesPhoto(data.subjectSpecies, incident.id))} alt="" fill sizes="80px" className="object-cover" />
          {data.isFlockLevel && (
            <span className="absolute bottom-1.5 right-1.5 flex size-6 items-center justify-center rounded-full bg-ink/80 text-cream" title="Whole group">
              <Users className="size-3.5" aria-hidden />
            </span>
          )}
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-muted">{data.subjectName ?? "Whole farm"}{data.subjectSpecies ? ` · ${data.subjectSpecies}` : ""}</p>
          <div className="mt-2 flex flex-wrap gap-1.5">
            <SeverityBadge severity={incident.severity} />
            <IncidentStatusBadge status={incident.status} />
            {data.isFlockLevel && <Badge tone="neutral">Whole group</Badge>}
          </div>
          {incident.followUpDate && incident.status !== IncidentStatus.Resolved && (
            <p className="mt-2 flex items-center gap-1.5 text-sm font-semibold text-olive-ink">
              <CalendarClock className="size-4" aria-hidden /> Follow-up {formatRelative(incident.followUpDate)}
            </p>
          )}
        </div>
      </div>
      <div className="border-t border-line px-5 py-4">
        <p className="text-[15px] leading-relaxed">{incident.evidenceSummary}</p>
      </div>
      <div className="flex flex-wrap gap-x-5 gap-y-2 border-t border-line px-5 py-3 text-sm text-muted">
        <span className="inline-flex items-center gap-1.5">
          <MapPin className="size-4" aria-hidden /> {data.farmLocation}
        </span>
        {data.farmer && (
          <>
            <span className="font-semibold text-ink">{data.farmer.name}</span>
            {data.farmer.phone && (
              <a href={`tel:${data.farmer.phone.replace(/\s+/g, "")}`} className="inline-flex items-center gap-1.5 font-semibold text-terracotta-ink hover:underline">
                <Phone className="size-4" aria-hidden /> {data.farmer.phone}
              </a>
            )}
          </>
        )}
      </div>
    </section>
  );
}

function VetSection({ data, onRequest }: { data: IncidentCase; onRequest: () => void }) {
  const closed = data.incident.status === IncidentStatus.Resolved;
  return (
    <section>
      <h2 className="mb-3 text-[19px] font-semibold tracking-[-0.02em]">Veterinary help</h2>
      {data.vetRequests.length === 0 ? (
        data.canRequestVet ? (
          <div className="relative overflow-hidden rounded-[28px] bg-ink p-6 text-cream">
            <div className="pointer-events-none absolute -right-12 -top-12 size-48 rounded-full bg-terracotta/25 blur-2xl" aria-hidden />
            <Stethoscope className="size-7 text-terracotta-soft" aria-hidden />
            <p className="relative mt-3 text-lg font-semibold">{data.incident.severity >= 2 ? "This case should be seen by a vet." : "Want a vet's opinion?"}</p>
            <p className="relative mt-1 max-w-md text-sm text-cream/70">Request a remote review or a farm visit. You only pay after a vet accepts.</p>
            <Button variant="accent" className="relative mt-4" icon={<Stethoscope className="size-4" />} onClick={onRequest}>
              Ask a vet
            </Button>
          </div>
        ) : (
          <p className="rounded-[24px] bg-paper px-5 py-4 text-sm text-muted ring-1 ring-line/60">{closed ? "This case was closed without a vet request." : "No vet requests on this case."}</p>
        )
      ) : (
        <div className="space-y-3">
          {data.vetRequests.map((r) => (
            <RequestCard key={r.id} request={r} incidentId={data.incident.id} />
          ))}
          {data.canRequestVet && (
            <Button variant="secondary" icon={<Plus className="size-4" />} onClick={onRequest}>
              New vet request
            </Button>
          )}
        </div>
      )}
    </section>
  );
}

function RequestCard({ request, incidentId }: { request: VetRequest; incidentId: string }) {
  const client = useQueryClient();
  const [dialog, setDialog] = useState<"cancel" | "complete" | "plan" | null>(null);
  const [notes, setNotes] = useState("");
  const invalidate = () => Promise.all([client.invalidateQueries({ queryKey: qk.incidentCase(incidentId) }), client.invalidateQueries({ queryKey: ["vet-requests"] })]);

  const cancel = useMutation({
    mutationFn: () => vetsApi.cancel(request.id),
    onSuccess: async () => {
      toast.success("Request cancelled");
      setDialog(null);
      await invalidate();
    },
  });
  const complete = useMutation({
    mutationFn: () => vetsApi.complete(request.id, notes.trim() || null),
    onSuccess: async () => {
      toast.success("Request marked complete");
      setDialog(null);
      await invalidate();
    },
  });

  const can = (action: string) => request.availableActions.includes(action);

  return (
    <article className="rounded-[28px] bg-paper p-5 ring-1 ring-line/60">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-[17px] font-semibold">{requestTypeMeta[request.requestType]?.label}</p>
          <p className="text-sm text-muted">
            {request.vet ? `${request.vet.name} · ${request.vet.specialization}` : request.isTargeted ? "Waiting for the chosen vet" : "Open to any available vet"}
          </p>
        </div>
        <div className="flex flex-wrap gap-1.5">
          <RequestStatusBadge status={request.status} />
          {request.farmerCost > 0 && <PaymentStatusBadge status={request.paymentStatus} mode={request.latestPayment?.mode} />}
        </div>
      </div>
      <dl className="mt-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
        <div>
          <dt className="text-muted">Fee</dt>
          <dd className="font-semibold">{request.farmerCost > 0 ? formatMoney(request.farmerCost, request.currency) : "No fee"}</dd>
        </div>
        <div>
          <dt className="text-muted">Requested</dt>
          <dd className="font-semibold">{formatRelative(request.createdAt)}</dd>
        </div>
        {request.preferredVisitDate && (
          <div>
            <dt className="text-muted">Preferred date</dt>
            <dd className="font-semibold">{formatDate(request.preferredVisitDate)}</dd>
          </div>
        )}
        {request.respondedAt && (
          <div>
            <dt className="text-muted">Accepted</dt>
            <dd className="font-semibold">{formatRelative(request.respondedAt)}</dd>
          </div>
        )}
      </dl>
      {request.description && <p className="mt-3 rounded-2xl bg-sand px-4 py-3 text-sm leading-relaxed">{request.description}</p>}
      {request.vet?.contact && request.status === 1 && (
        <p className="mt-3 text-sm">
          <span className="text-muted">Vet contact: </span>
          <span className="font-semibold">{request.vet.contact}</span>
        </p>
      )}
      {request.declineReason && <p className="mt-3 text-sm text-muted">Declined: {request.declineReason}</p>}
      {request.completionNotes && (
        <p className="mt-3 rounded-2xl bg-green-soft px-4 py-3 text-sm text-green-ink">
          <span className="font-semibold">Vet&apos;s notes: </span>
          {request.completionNotes}
        </p>
      )}

      {(can("pay") || can("cancel") || can("treatment_plan") || can("complete")) && (
        <div className="mt-4 flex flex-wrap gap-2">
          {can("pay") && (
            <ButtonLink href={`/pay/${request.id}`} variant="accent" icon={<Wallet className="size-4" />}>
              Review and pay {formatMoney(request.farmerCost, request.currency)}
            </ButtonLink>
          )}
          {can("treatment_plan") && (
            <Button variant="primary" icon={<ClipboardCheck className="size-4" />} onClick={() => setDialog("plan")}>
              Add treatment plan
            </Button>
          )}
          {can("complete") && (
            <Button variant="secondary" icon={<CheckCircle2 className="size-4" />} onClick={() => setDialog("complete")}>
              Mark visit complete
            </Button>
          )}
          {can("cancel") && (
            <Button variant="ghost" icon={<X className="size-4" />} onClick={() => setDialog("cancel")}>
              Cancel request
            </Button>
          )}
        </div>
      )}
      {request.status === 1 && request.farmerCost > 0 && request.paymentStatus !== "Paid" && !can("pay") && request.availableActions.includes("treatment_plan") && (
        <p className="mt-3 text-xs text-muted">You can mark the visit complete once the farmer&apos;s payment is confirmed.</p>
      )}

      <ConfirmDialog
        open={dialog === "cancel"}
        onOpenChange={(o) => !o && setDialog(null)}
        title="Cancel this request?"
        description="The vet will no longer see it. You can make a new request later."
        confirmLabel="Cancel request"
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
      />
      <Modal open={dialog === "complete"} onOpenChange={(o) => !o && setDialog(null)} title="Mark the request complete" description="Summarise what you did. The farmer sees this in the case.">
        <Field label="Notes" optional>
          <Textarea value={notes} onChange={(e) => setNotes(e.target.value.slice(0, 1000))} placeholder="Examined the flock, started treatment, re-check in two weeks." />
        </Field>
        <InlineError error={complete.error} className="mt-3" />
        <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <Button variant="secondary" onClick={() => setDialog(null)}>
            Back
          </Button>
          <Button loading={complete.isPending} onClick={() => complete.mutate()}>
            Mark complete
          </Button>
        </div>
      </Modal>
      {dialog === "plan" && <TreatmentPlanModal open onOpenChange={(o) => !o && setDialog(null)} vetRequestId={request.id} incidentId={incidentId} />}
    </article>
  );
}

function TreatmentPlans({ plans, incidentId, canWrite }: { plans: TreatmentPlan[]; incidentId: string; canWrite: boolean }) {
  const client = useQueryClient();
  const [now] = useState(() => Date.now());
  const step = useMutation({
    mutationFn: ({ planId, stepId }: { planId: string; stepId: string }) => vetsApi.completeTreatmentStep(planId, stepId),
    onSuccess: () => client.invalidateQueries({ queryKey: qk.incidentCase(incidentId) }),
    onError: (e) => toast.error(e instanceof Error ? e.message : "Could not update the step"),
  });
  return (
    <section>
      <h2 className="mb-3 text-[19px] font-semibold tracking-[-0.02em]">Treatment plan</h2>
      <div className="space-y-3">
        {plans.map((plan) => (
          <Card key={plan.id}>
            <p className="text-sm text-muted">
              From {plan.authorName ?? "your vet"} · {formatDate(plan.createdAt)}
            </p>
            <div className="mt-1 flex items-center justify-between">
              <p className="whitespace-pre-line text-[15px] leading-relaxed">{plan.instructionsText}</p>
            </div>
            <ListenButton text={plan.instructionsText} label="Listen to instructions" variant="pill" className="mt-3" />
            {plan.schedule.length > 0 && (
              <ol className="mt-4 space-y-2">
                {plan.schedule.map((s) => {
                  const overdue = !s.isCompleted && new Date(s.dueDate).getTime() < now - 86_400_000;
                  return (
                    <li key={s.id} className="flex items-center gap-3 rounded-2xl bg-sand px-3 py-2.5">
                      <button
                        type="button"
                        disabled={!canWrite || s.isCompleted || step.isPending}
                        onClick={() => step.mutate({ planId: plan.id, stepId: s.id })}
                        aria-label={s.isCompleted ? `${s.action} — done` : `Mark “${s.action}” done`}
                        className={cn(
                          "flex size-7 shrink-0 items-center justify-center rounded-full ring-1 transition-colors",
                          s.isCompleted ? "bg-green text-white ring-green" : "bg-paper ring-line hover:ring-ink disabled:hover:ring-line",
                        )}
                      >
                        {s.isCompleted && <Check className="size-4" aria-hidden />}
                      </button>
                      <span className={cn("min-w-0 flex-1 text-sm font-medium", s.isCompleted && "text-muted line-through")}>{s.action}</span>
                      <span className={cn("shrink-0 text-xs", overdue ? "font-semibold text-terracotta-deep" : "text-muted")}>
                        {s.isCompleted ? `Done ${formatDate(s.completedAt, { day: "numeric", month: "short" })}` : formatDate(s.dueDate, { day: "numeric", month: "short" })}
                      </span>
                    </li>
                  );
                })}
              </ol>
            )}
          </Card>
        ))}
      </div>
    </section>
  );
}

function ActionsSection({ data, canWrite }: { data: IncidentCase; canWrite: boolean }) {
  const client = useQueryClient();
  const [description, setDescription] = useState("");
  const [priority, setPriority] = useState("Medium");
  const invalidate = () => client.invalidateQueries({ queryKey: qk.incidentCase(data.incident.id) });

  const add = useMutation({
    mutationFn: () => incidentsApi.addAction(data.incident.id, description.trim(), priority),
    onSuccess: async () => {
      setDescription("");
      await invalidate();
    },
  });
  const complete = useMutation({
    mutationFn: (actionId: string) => incidentsApi.completeAction(data.incident.id, actionId),
    onSuccess: invalidate,
    onError: (e) => toast.error(e instanceof Error ? e.message : "Could not update the action"),
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    if (description.trim().length >= 3) add.mutate();
  }

  const open = data.actions.filter((a) => !a.completedAt);
  const done = data.actions.filter((a) => a.completedAt);

  return (
    <section>
      <h2 className="mb-3 text-[19px] font-semibold tracking-[-0.02em]">Actions</h2>
      <Card>
        {data.actions.length === 0 && <p className="mb-3 text-sm text-muted">No actions yet. Add the practical steps — isolate birds, fix drinkers, buy medicine.</p>}
        <ul className="space-y-2">
          {[...open, ...done].map((a) => (
            <li key={a.id} className="flex items-center gap-3 rounded-2xl px-1 py-1.5">
              <button
                type="button"
                disabled={!canWrite || Boolean(a.completedAt) || complete.isPending}
                onClick={() => complete.mutate(a.id)}
                aria-label={a.completedAt ? `${a.description} — done` : `Mark “${a.description}” done`}
                className={cn("flex size-7 shrink-0 items-center justify-center rounded-full ring-1", a.completedAt ? "bg-green text-white ring-green" : "ring-line hover:ring-ink disabled:hover:ring-line")}
              >
                {a.completedAt && <Check className="size-4" aria-hidden />}
              </button>
              <span className={cn("min-w-0 flex-1 text-[15px]", a.completedAt && "text-muted line-through")}>{a.description}</span>
              <Badge tone={a.priority === "High" ? "concerning" : "neutral"}>{a.priority}</Badge>
            </li>
          ))}
        </ul>
        {canWrite && (
          <form onSubmit={submit} className="mt-4 flex flex-col gap-2 border-t border-line pt-4 sm:flex-row">
            <Input value={description} onChange={(e) => setDescription(e.target.value.slice(0, 500))} placeholder="Add an action…" aria-label="New action" />
            <div className="flex gap-2">
              <Select value={priority} onChange={(e) => setPriority(e.target.value)} aria-label="Priority" className="w-32">
                <option>High</option>
                <option>Medium</option>
                <option>Low</option>
              </Select>
              <Button type="submit" loading={add.isPending} disabled={description.trim().length < 3} icon={<Plus className="size-4" />}>
                Add
              </Button>
            </div>
          </form>
        )}
        <InlineError error={add.error} className="mt-3" />
      </Card>
    </section>
  );
}

function FollowUpsSection({ data, canWrite }: { data: IncidentCase; canWrite: boolean }) {
  const client = useQueryClient();
  const [outcome, setOutcome] = useState<number | null>(null);
  const [notes, setNotes] = useState("");
  const add = useMutation({
    mutationFn: () => incidentsApi.addFollowUp(data.incident.id, outcome!, notes.trim() || null),
    onSuccess: async () => {
      toast.success("Follow-up recorded");
      setOutcome(null);
      setNotes("");
      await Promise.all([client.invalidateQueries({ queryKey: qk.incidentCase(data.incident.id) }), client.invalidateQueries({ queryKey: ["overview"] })]);
    },
  });

  return (
    <section id="follow-up" className="scroll-mt-24">
      <h2 className="mb-3 text-[19px] font-semibold tracking-[-0.02em]">Follow-ups</h2>
      <Card>
        {data.followUps.length > 0 && (
          <ol className="mb-4 space-y-3">
            {data.followUps.map((f) => (
              <li key={f.id} className="flex items-start gap-3">
                <Badge tone={outcomeMeta[f.farmerResponse]?.tone ?? "neutral"} dot>
                  {outcomeMeta[f.farmerResponse]?.label}
                </Badge>
                <div className="min-w-0 flex-1 text-sm">
                  {f.notes && <p className="leading-relaxed">{f.notes}</p>}
                  <p className="text-xs text-faint">{formatDateTime(f.recordedAt)}</p>
                </div>
              </li>
            ))}
          </ol>
        )}
        {canWrite ? (
          <div className={cn(data.followUps.length > 0 && "border-t border-line pt-4")}>
            <p className="mb-2 text-sm font-semibold">How are the animals doing?</p>
            <ChoiceChips
              label="Outcome"
              value={outcome}
              onChange={setOutcome}
              options={[Outcome.Improving, Outcome.Worsening, Outcome.Resolved].map((o) => ({ value: o, label: outcomeMeta[o]!.label }))}
            />
            {outcome !== null && (
              <div className="mt-3 space-y-3">
                <Textarea value={notes} onChange={(e) => setNotes(e.target.value.slice(0, 1000))} placeholder="What changed? Deaths, appetite, behaviour…" aria-label="Follow-up notes" className="min-h-20" />
                {outcome === Outcome.Worsening && <Notice tone="warning">If animals are getting worse, ask a vet — don&apos;t wait for the next follow-up.</Notice>}
                <InlineError error={add.error} />
                <Button loading={add.isPending} onClick={() => add.mutate()}>
                  Save follow-up
                </Button>
              </div>
            )}
          </div>
        ) : (
          data.followUps.length === 0 && <p className="text-sm text-muted">No follow-ups recorded.</p>
        )}
      </Card>
    </section>
  );
}

function StatusControl({ data }: { data: IncidentCase }) {
  const client = useQueryClient();
  const [target, setTarget] = useState<number | null>(null);
  const [date, setDate] = useState(() => new Date(Date.now() + 86_400_000).toISOString().slice(0, 10));

  const change = useMutation({
    mutationFn: (status: number) => incidentsApi.setStatus(data.incident.id, status, status === IncidentStatus.FollowUpRequired || status === IncidentStatus.UnderMonitoring ? new Date(`${date}T09:00:00`).toISOString() : null),
    onSuccess: async (_, status) => {
      toast.success(`Case ${incidentStatusMeta[status]?.label.toLowerCase()}`);
      setTarget(null);
      await Promise.all([client.invalidateQueries({ queryKey: qk.incidentCase(data.incident.id) }), client.invalidateQueries({ queryKey: ["incidents"] }), client.invalidateQueries({ queryKey: ["overview"] })]);
    },
  });

  const needsDate = target === IncidentStatus.FollowUpRequired || target === IncidentStatus.UnderMonitoring;

  return (
    <Card>
      <CardTitle>Case status</CardTitle>
      <div className="mb-4 flex items-center gap-2">
        <IncidentStatusBadge status={data.incident.status} />
        {data.incident.resolvedAt && <span className="text-xs text-muted">Resolved {formatDate(data.incident.resolvedAt)}</span>}
      </div>
      <div className="flex flex-wrap gap-2">
        {data.allowedTransitions.map((s) => (
          <Button key={s} size="sm" variant={s === IncidentStatus.Resolved ? "primary" : "secondary"} onClick={() => setTarget(s)} icon={s === IncidentStatus.Resolved ? <CheckCircle2 className="size-4" /> : undefined}>
            {incidentStatusMeta[s]?.verb}
          </Button>
        ))}
      </div>
      <Modal
        open={target !== null}
        onOpenChange={(o) => !o && setTarget(null)}
        title={target !== null ? `${incidentStatusMeta[target]?.verb}?` : ""}
        description={
          target === IncidentStatus.Resolved
            ? "Resolve the case when the animals have recovered. You can reopen it if signs come back."
            : target === IncidentStatus.FollowUpRequired
              ? "FarmHand will remind you to record how the animals are doing."
              : target === IncidentStatus.UnderMonitoring
                ? "Keep watching. Add a check-in date if you want a reminder."
                : undefined
        }
        footer={
          <>
            <Button variant="secondary" onClick={() => setTarget(null)}>
              Back
            </Button>
            <Button loading={change.isPending} onClick={() => target !== null && change.mutate(target)}>
              {target !== null ? incidentStatusMeta[target]?.verb : "Save"}
            </Button>
          </>
        }
      >
        {needsDate && (
          <Field label="Check-in date">
            <Input type="date" value={date} min={new Date().toISOString().slice(0, 10)} onChange={(e) => setDate(e.target.value)} />
          </Field>
        )}
        <InlineError error={change.error} className="mt-3" />
      </Modal>
    </Card>
  );
}

function PreventionSection({ data, canWrite }: { data: IncidentCase; canWrite: boolean }) {
  const client = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [content, setContent] = useState(data.preventionPlan?.content ?? "");
  const save = useMutation({
    mutationFn: () => incidentsApi.savePreventionPlan(data.incident.id, content.trim()),
    onSuccess: async () => {
      toast.success("Prevention notes saved");
      setEditing(false);
      await client.invalidateQueries({ queryKey: qk.incidentCase(data.incident.id) });
    },
  });

  if (!data.preventionPlan && !canWrite) return null;

  return (
    <Card>
      <CardTitle action={<ShieldPlus className="size-4 text-faint" aria-hidden />}>Prevention</CardTitle>
      {editing ? (
        <div className="space-y-3">
          <Textarea value={content} onChange={(e) => setContent(e.target.value.slice(0, 4000))} className="min-h-32" placeholder="Biosecurity, vaccination, housing and feeding changes to stop this happening again." />
          <InlineError error={save.error} />
          <div className="flex gap-2">
            <Button size="sm" loading={save.isPending} disabled={content.trim().length < 5} onClick={() => save.mutate()}>
              Save
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
              Cancel
            </Button>
          </div>
        </div>
      ) : data.preventionPlan ? (
        <>
          <p className="whitespace-pre-line text-[15px] leading-relaxed">{data.preventionPlan.content}</p>
          {canWrite && (
            <Button size="sm" variant="ghost" className="mt-3 -ml-3" onClick={() => setEditing(true)}>
              Edit
            </Button>
          )}
        </>
      ) : (
        <>
          <p className="text-sm text-muted">Write down what will stop this from happening again.</p>
          <Button size="sm" variant="secondary" className="mt-3" icon={<Plus className="size-4" />} onClick={() => setEditing(true)}>
            Add prevention notes
          </Button>
        </>
      )}
    </Card>
  );
}

function Timeline({ data }: { data: IncidentCase }) {
  if (data.timeline.length === 0) return null;
  return (
    <Card>
      <CardTitle>Timeline</CardTitle>
      <ol className="relative space-y-4 before:absolute before:bottom-2 before:left-[5px] before:top-2 before:w-px before:bg-line">
        {[...data.timeline].reverse().map((t, i) => (
          <li key={`${t.at}-${i}`} className="relative pl-6">
            <span className={cn("absolute left-0 top-1.5 size-[11px] rounded-full ring-2 ring-paper", t.kind === "resolved" ? "bg-green" : t.kind === "vet" || t.kind === "treatment" ? "bg-terracotta" : "bg-beige")} aria-hidden />
            <p className="text-sm font-semibold leading-snug">{t.title}</p>
            {t.detail && <p className="mt-0.5 line-clamp-3 text-[13px] text-muted">{t.detail}</p>}
            <p className="mt-0.5 text-xs text-faint">
              {formatDateTime(t.at)}
              {t.actor ? ` · ${t.actor}` : ""}
            </p>
          </li>
        ))}
      </ol>
      {data.sourceObservation?.analysisSource && (
        <div className="mt-4 border-t border-line pt-3">
          <SourceBadge source={data.sourceObservation.analysisSource} />
        </div>
      )}
    </Card>
  );
}
