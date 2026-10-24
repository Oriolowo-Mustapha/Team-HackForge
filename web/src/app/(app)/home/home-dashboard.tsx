"use client";

import { useQuery } from "@tanstack/react-query";
import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  CalendarClock,
  CheckCircle2,
  ClipboardList,
  CloudOff,
  HeartPulse,
  Plus,
  ShieldCheck,
  Siren,
  Stethoscope,
  Tractor,
} from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";
import { farmsApi } from "@/lib/api/endpoints";
import type { Dashboard } from "@/lib/api/schemas";
import { cn } from "@/lib/cn";
import { listDrafts, type ReportDraft } from "@/lib/drafts";
import { firstName, formatDate, formatNumber, formatRelative, greeting, pluralize } from "@/lib/format";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { useSession } from "@/lib/hooks/use-session";
import { qk } from "@/lib/queries";
import { CheckInArc } from "@/components/charts/check-in-arc";
import { DailyColumns } from "@/components/charts/daily-columns";
import { LabeledBars } from "@/components/charts/labeled-bars";
import { IncidentMiniRow, ObservationRow, VetRequestRow } from "@/components/domain/rows";
import { ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { SectionHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Stat } from "@/components/ui/stat";

function nextActionHref(data: Dashboard): { href: string; label: string } {
  switch (data.nextActionKind) {
    case "add_livestock":
      return { href: "/animals/new", label: "Add livestock" };
    case "request_vet": {
      const urgent = data.attentionIncidents.find((i) => i.severity >= 2 && !data.activeVetRequests.some((r) => r.incidentId === i.id));
      return { href: urgent ? `/cases/${urgent.id}?request=1` : "/cases", label: "Ask a vet" };
    }
    case "review_observation":
      return { href: data.pendingObservations[0] ? `/reports/${data.pendingObservations[0].id}` : "/reports", label: "Review report" };
    case "pay": {
      const toPay = data.activeVetRequests.find((r) => r.availableActions.includes("pay"));
      return { href: toPay ? `/pay/${toPay.id}` : "/payments", label: "Review and pay" };
    }
    case "follow_up": {
      const overdue = data.reminders.find((r) => r.isOverdue);
      return { href: overdue ? `/cases/${overdue.incidentId}#follow-up` : "/cases", label: "Record follow-up" };
    }
    default:
      return { href: "/scan", label: "Start a health check" };
  }
}

const severityIcons = [ShieldCheck, AlertCircle, AlertTriangle, Siren];
const severityNames = ["Low", "Medium", "High", "Critical"];

export function HomeDashboard() {
  const { user } = useSession();
  const ownerUserId = user?.userId ?? "";
  const { farm } = useActiveFarm();
  const [drafts, setDrafts] = useState<ReportDraft[]>([]);

  const overview = useQuery({
    queryKey: qk.overview(farm?.id ?? "none"),
    queryFn: ({ signal }) => farmsApi.overview(farm!.id, signal),
    enabled: Boolean(farm),
  });

  useEffect(() => {
    let cancelled = false;
    listDrafts(ownerUserId)
      .then((all) => { if (!cancelled) setDrafts(all.filter((d) => d.farmId === farm?.id)); })
      .catch(() => { if (!cancelled) setDrafts([]); });
    return () => { cancelled = true; };
  }, [farm?.id, ownerUserId]);

  const now = new Date();

  return (
    <div className="space-y-6">
      <div className="flex items-end justify-between gap-4 pt-1">
        <div>
          <p className="text-[13px] font-semibold text-muted">{formatDate(now, { weekday: "long", day: "numeric", month: "long" })}</p>
          <h1 className="mt-1 text-[28px] font-semibold leading-tight tracking-[-0.03em] sm:text-[34px]">
            {greeting(now)}, {firstName(user?.fullName)}
          </h1>
        </div>
        <ButtonLink href="/scan" variant="accent" className="hidden sm:inline-flex lg:hidden" icon={<HeartPulse className="size-5" />}>
          Report a concern
        </ButtonLink>
      </div>

      {overview.isPending ? (
        <DashboardSkeleton />
      ) : overview.isError ? (
        <ErrorState error={overview.error} onRetry={() => void overview.refetch()} />
      ) : (
        <DashboardBody data={overview.data} drafts={drafts.filter((draft) => draft.ownerUserId === ownerUserId && draft.farmId === farm?.id)} />
      )}
    </div>
  );
}

function DashboardBody({ data, drafts }: { data: Dashboard; drafts: ReportDraft[] }) {
  const next = nextActionHref(data);
  const urgentKinds = ["request_vet", "pay", "follow_up", "review_observation"];
  const urgent = urgentKinds.includes(data.nextActionKind);
  const totalAnimals = data.counts.animals + data.counts.flockHeadcount;
  const deaths14 = data.last14Days.reduce((sum, d) => sum + d.deaths, 0);
  const hasCases = data.counts.openIncidents > 0 || data.counts.resolvedLast30Days > 0;

  return (
    <div className="grid gap-6 lg:grid-cols-12">
      <div className="space-y-6 lg:col-span-7 xl:col-span-8">
        {/* Next best action */}
        <section
          aria-label="What to do next"
          className={cn("relative overflow-hidden rounded-[32px] p-6 sm:p-7", urgent ? "bg-ink text-cream" : "bg-paper ring-1 ring-line/60")}
        >
          {urgent && (
            <div className="pointer-events-none absolute -right-10 -top-10 size-48 rounded-full bg-terracotta/25 blur-2xl" aria-hidden />
          )}
          <p className={cn("text-[13px] font-semibold uppercase tracking-[0.08em]", urgent ? "text-terracotta-soft" : "text-faint")}>Next step</p>
          <p className="relative mt-2 max-w-xl text-[21px] font-semibold leading-snug tracking-[-0.02em] text-balance sm:text-[24px]">{data.nextAction}</p>
          <ButtonLink href={next.href} variant={urgent ? "accent" : "primary"} className="relative mt-5" trailingIcon={<ArrowRight className="size-4" />}>
            {next.label}
          </ButtonLink>
        </section>

        {drafts.length > 0 && (
          <Link href="/scan?drafts=1" className="flex items-center gap-3 rounded-[24px] bg-olive-soft p-4 text-olive-ink hover:brightness-[0.98]">
            <CloudOff className="size-5 shrink-0" aria-hidden />
            <span className="flex-1 text-sm font-semibold">
              {pluralize(drafts.length, "unsent report")} saved on this device. Open to send {drafts.length === 1 ? "it" : "them"}.
            </span>
            <ArrowRight className="size-4" aria-hidden />
          </Link>
        )}

        {/* Stats */}
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Stat label="Animals" value={formatNumber(totalAnimals)} context={`${pluralize(data.counts.groups, "group")}`} icon={<Tractor />} href="/animals" />
          <Stat label="Open cases" value={data.counts.openIncidents} context={data.counts.urgentIncidents ? `${data.counts.urgentIncidents} high or critical` : "None urgent"} icon={<ClipboardList />} href="/cases" emphasis={data.counts.urgentIncidents > 0} />
          <Stat label="To review" value={data.counts.pendingObservations} context="reports" icon={<CheckCircle2 />} href="/reports?status=pending" />
          <Stat label="Vet requests" value={data.counts.activeVetRequests} context="active" icon={<Stethoscope />} href="/cases?vet=1" />
        </div>

        {/* Attention */}
        <section>
          <SectionHeader title="Needs attention" count={data.attentionIncidents.length} href="/cases" />
          {data.attentionIncidents.length ? (
            <div className="grid gap-2.5 sm:grid-cols-2">
              {data.attentionIncidents.map((incident) => (
                <IncidentMiniRow key={incident.id} incident={incident} />
              ))}
            </div>
          ) : (
            <EmptyState
              icon={<ShieldCheck />}
              title={hasCases ? "No open cases" : "No cases yet"}
              className="py-8"
              image={
                <div className="relative mb-4 h-24 w-40 overflow-hidden rounded-[20px]">
                  <Image src="/images/hen-free-range-800.webp" alt="" fill sizes="160px" className="object-cover" />
                </div>
              }
            >
              When a report looks concerning, FarmHand opens a case so you can track actions, follow-ups and vet visits.
            </EmptyState>
          )}
        </section>

        {data.pendingObservations.length > 0 && (
          <section>
            <SectionHeader title="Waiting for your review" count={data.pendingObservations.length} href="/reports?status=pending" />
            <div className="space-y-2.5">
              {data.pendingObservations.map((o) => (
                <ObservationRow key={o.id} item={o} />
              ))}
            </div>
          </section>
        )}

        {data.activeVetRequests.length > 0 && (
          <section>
            <SectionHeader title="Vet requests" count={data.activeVetRequests.length} />
            <div className="space-y-2.5">
              {data.activeVetRequests.map((r) => (
                <VetRequestRow key={r.id} request={r} href={r.availableActions.includes("pay") ? `/pay/${r.id}` : `/cases/${r.incidentId}`} />
              ))}
            </div>
          </section>
        )}

        <section>
          <SectionHeader title="Recent activity" href="/reports" linkLabel="All reports" />
          {data.recentActivity.length ? (
            <Card padded={false}>
              <ol className="divide-y divide-line/70">
                {data.recentActivity.map((a, i) => {
                  const href = a.relatedId ? (a.kind === "observation" ? `/reports/${a.relatedId}` : `/cases/${a.relatedId}`) : null;
                  const body = (
                    <>
                      <span className="mt-1.5 size-2 shrink-0 rounded-full bg-beige" aria-hidden />
                      <span className="min-w-0 flex-1">
                        <span className="block text-[15px] font-semibold leading-snug">{a.title}</span>
                        {a.detail && <span className="mt-0.5 line-clamp-2 block text-[13px] text-muted">{a.detail}</span>}
                      </span>
                      <time className="shrink-0 text-xs text-faint" dateTime={a.at}>
                        {formatRelative(a.at)}
                      </time>
                    </>
                  );
                  return (
                    <li key={`${a.at}-${i}`}>
                      {href ? (
                        <Link href={href} className="flex items-start gap-3 px-5 py-3.5 hover:bg-sand/60">
                          {body}
                        </Link>
                      ) : (
                        <div className="flex items-start gap-3 px-5 py-3.5">{body}</div>
                      )}
                    </li>
                  );
                })}
              </ol>
            </Card>
          ) : (
            <EmptyState icon={<HeartPulse />} title="Nothing recorded yet" action={<ButtonLink href="/scan" variant="accent">Record a health check</ButtonLink>}>
              A daily check takes less than a minute — say what you see, or snap a photo.
            </EmptyState>
          )}
        </section>
      </div>

      <aside className="space-y-6 lg:col-span-5 xl:col-span-4" aria-label="Farm health summary">
        <Card className="pb-4">
          <CardTitle action={<Link href="/scan" className="rounded-lg text-sm font-semibold text-terracotta-ink hover:underline">Check now</Link>}>
            Daily checks
          </CardTitle>
          <CheckInArc days={data.last14Days} />
          <p className="mt-1 text-center text-[13px] text-muted">
            {data.counts.observationsLast7Days > 0 ? `${pluralize(data.counts.observationsLast7Days, "report")} in the last 7 days` : "No reports in the last 7 days"}
          </p>
        </Card>

        <Card>
          <CardTitle>Deaths recorded</CardTitle>
          <p className="-mt-2 mb-4 text-[13px] text-muted">
            {deaths14 === 0 ? "None in the last 14 days." : `${pluralize(deaths14, "death")} across all reports in the last 14 days.`}
          </p>
          <DailyColumns points={data.last14Days.map((d) => ({ date: d.date, value: d.deaths }))} label="Deaths per day, last 14 days" unit={["death", "deaths"]} />
        </Card>

        <Card>
          <CardTitle>Open cases by severity</CardTitle>
          <LabeledBars
            emptyLabel="No open cases."
            rows={[...data.openBySeverity]
              .sort((a, b) => b.severity - a.severity)
              .map((s) => {
                const Icon = severityIcons[s.severity] ?? ShieldCheck;
                return { label: severityNames[s.severity] ?? String(s.severity), value: s.count, icon: <Icon className="size-4 text-muted" aria-hidden /> };
              })}
          />
        </Card>

        {data.reminders.length > 0 && (
          <Card>
            <CardTitle>Coming up</CardTitle>
            <ul className="space-y-2">
              {data.reminders.map((r) => (
                <li key={`${r.incidentId}-${r.kind}-${r.dueAt}`}>
                  <Link href={`/cases/${r.incidentId}`} className="flex items-start gap-3 rounded-2xl p-2 hover:bg-sand">
                    <span className={cn("flex size-10 shrink-0 items-center justify-center rounded-xl", r.isOverdue ? "bg-terracotta-soft text-terracotta-deep" : "bg-sand")}>
                      <CalendarClock className="size-[18px]" aria-hidden />
                    </span>
                    <span className="min-w-0">
                      <span className="block text-sm font-semibold leading-snug">{r.title}</span>
                      <span className={cn("text-xs", r.isOverdue ? "font-semibold text-terracotta-deep" : "text-muted")}>
                        {r.isOverdue ? "Overdue · " : "Due "}
                        {formatRelative(r.dueAt)}
                      </span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        )}

        {data.counts.groups === 0 && data.counts.animals === 0 && (
          <EmptyState icon={<Plus />} title="Add your animals" action={<ButtonLink href="/animals/new">Add livestock</ButtonLink>}>
            Reports are linked to a flock, herd or individual animal.
          </EmptyState>
        )}
      </aside>
    </div>
  );
}

function DashboardSkeleton() {
  return (
    <div className="grid gap-6 lg:grid-cols-12" role="status" aria-label="Loading your farm">
      <div className="space-y-6 lg:col-span-7 xl:col-span-8">
        <Skeleton className="h-44 rounded-[32px]" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-[104px] rounded-[24px]" />
          ))}
        </div>
        <Skeleton className="h-40 rounded-[28px]" />
      </div>
      <div className="space-y-6 lg:col-span-5 xl:col-span-4">
        <Skeleton className="h-72 rounded-[28px]" />
        <Skeleton className="h-48 rounded-[28px]" />
      </div>
    </div>
  );
}
