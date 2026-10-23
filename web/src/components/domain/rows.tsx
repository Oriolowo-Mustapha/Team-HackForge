import { ChevronRight, ClipboardCheck, Mic, Camera, Type, Stethoscope, Users } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import type { ReactNode } from "react";
import type { Incident, IncidentListItem, ObservationSummary, VetRequest } from "@/lib/api/schemas";
import { requestTypeMeta } from "@/lib/api/enums";
import { cn } from "@/lib/cn";
import { formatMoney, formatRelative } from "@/lib/format";
import { smallPhoto, speciesPhoto } from "@/lib/images";
import { IncidentStatusBadge, ObservationStatusBadge, PaymentStatusBadge, RequestStatusBadge, SeverityBadge } from "./badges";

/** Rounded species photo used as a list thumbnail. */
export function SubjectThumb({ species, seed, className, flock }: { species: string | null | undefined; seed: string; className?: string; flock?: boolean }) {
  return (
    <span className={cn("relative block size-14 shrink-0 overflow-hidden rounded-[18px] bg-sand", className)}>
      <Image src={smallPhoto(speciesPhoto(species, seed))} alt="" fill sizes="64px" className="object-cover" />
      {flock && (
        <span className="absolute bottom-1 right-1 flex size-5 items-center justify-center rounded-full bg-ink/80 text-cream" title="Whole group">
          <Users className="size-3" aria-hidden />
        </span>
      )}
    </span>
  );
}

function RowLink({ href, children, className }: { href: string; children: ReactNode; className?: string }) {
  return (
    <Link href={href} className={cn("group flex items-center gap-3.5 rounded-[24px] bg-paper p-3 pr-4 ring-1 ring-line/60 transition-colors hover:bg-white", className)}>
      {children}
      <ChevronRight className="size-4 shrink-0 text-faint transition-transform group-hover:translate-x-0.5" aria-hidden />
    </Link>
  );
}

export function IncidentRow({ item, showFarm = false }: { item: IncidentListItem; showFarm?: boolean }) {
  const { incident } = item;
  return (
    <RowLink href={`/cases/${incident.id}`}>
      <SubjectThumb species={item.subjectSpecies} seed={incident.id} flock={item.isFlockLevel} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-semibold leading-snug">{incident.incidentType}</p>
        <p className="truncate text-[13px] text-muted">
          {[item.subjectName ?? (item.isFlockLevel ? "Whole farm" : null), showFarm ? item.farmName : null, formatRelative(incident.detectedAt)].filter(Boolean).join(" · ")}
        </p>
        <div className="mt-2 flex flex-wrap gap-1.5">
          <SeverityBadge severity={incident.severity} />
          <IncidentStatusBadge status={incident.status} />
          {item.vetRequestStatus !== null && <RequestStatusBadge status={item.vetRequestStatus} />}
        </div>
      </div>
    </RowLink>
  );
}

/** Compact incident row for dashboard cards when only the bare incident is available. */
export function IncidentMiniRow({ incident, subject }: { incident: Incident; subject?: string | null }) {
  return (
    <RowLink href={`/cases/${incident.id}`} className="p-3">
      <span className={cn("flex size-11 shrink-0 items-center justify-center rounded-2xl", incident.severity >= 2 ? "bg-terracotta-soft text-terracotta-deep" : "bg-sand text-ink")}>
        <Stethoscope className="size-5" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-semibold">{incident.incidentType}</p>
        <div className="mt-1 flex flex-wrap items-center gap-1.5">
          <SeverityBadge severity={incident.severity} />
          <span className="text-xs text-faint">{subject ?? formatRelative(incident.detectedAt)}</span>
        </div>
      </div>
    </RowLink>
  );
}

function channelIcons(channels: string | null) {
  const list = (channels ?? "").split(",").map((c) => c.trim()).filter(Boolean);
  return (
    <span className="flex items-center gap-1 text-faint" aria-label={list.length ? `Sent as ${list.join(", ")}` : undefined}>
      {list.includes("voice") && <Mic className="size-3.5" aria-hidden />}
      {list.includes("photo") && <Camera className="size-3.5" aria-hidden />}
      {(list.includes("text") || list.includes("manual")) && <Type className="size-3.5" aria-hidden />}
    </span>
  );
}

export function ObservationRow({ item }: { item: ObservationSummary }) {
  const pending = item.confirmationStatus === 0;
  return (
    <RowLink href={`/reports/${item.id}`}>
      <span className={cn("flex size-12 shrink-0 items-center justify-center rounded-2xl", pending ? "bg-olive-soft text-olive-ink" : "bg-sand text-ink")}>
        <ClipboardCheck className="size-5" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-semibold">{item.subjectName ?? "Farm report"}</p>
        <p className="line-clamp-1 text-[13px] text-muted">{item.farmerNotes ?? "No notes"}</p>
        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          <ObservationStatusBadge status={item.confirmationStatus} />
          {item.assessedSeverity !== null && <SeverityBadge severity={item.assessedSeverity} />}
          <span className="ml-1 text-xs text-faint">{formatRelative(item.recordedAt)}</span>
          {channelIcons(item.inputChannels)}
        </div>
      </div>
    </RowLink>
  );
}

export function VetRequestRow({ request, href }: { request: VetRequest; href: string }) {
  return (
    <RowLink href={href}>
      <SubjectThumb species={request.subjectSpecies} seed={request.incidentId} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-semibold">{request.incidentType}</p>
        <p className="truncate text-[13px] text-muted">
          {requestTypeMeta[request.requestType]?.label} · {request.vet?.name ?? (request.isTargeted ? "Requested vet" : "Any available vet")} · {formatMoney(request.farmerCost, request.currency)}
        </p>
        <div className="mt-2 flex flex-wrap gap-1.5">
          <RequestStatusBadge status={request.status} />
          {request.farmerCost > 0 && request.paymentStatus !== "NotRequired" && <PaymentStatusBadge status={request.paymentStatus} mode={request.latestPayment?.mode} />}
        </div>
      </div>
    </RowLink>
  );
}
