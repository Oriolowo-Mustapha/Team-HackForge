import { AlertCircle, AlertTriangle, CheckCircle2, FlaskConical, ListChecks, PenLine, ShieldCheck, Siren, Sparkles } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { incidentStatusMeta, observationStatusMeta, paymentStatusMeta, requestStatusMeta, severityMeta, sourceLabel } from "@/lib/api/enums";

const severityIcons = [ShieldCheck, AlertCircle, AlertTriangle, Siren];

export function SeverityBadge({ severity, className, prefix }: { severity: number; className?: string; prefix?: string }) {
  const meta = severityMeta[severity] ?? severityMeta[0]!;
  const Icon = severityIcons[severity] ?? ShieldCheck;
  return (
    <Badge tone={meta.tone} icon={<Icon className="size-3.5" aria-hidden />} className={className}>
      {prefix ? `${prefix} ` : ""}
      {meta.label}
    </Badge>
  );
}

export function IncidentStatusBadge({ status }: { status: number }) {
  const meta = incidentStatusMeta[status] ?? incidentStatusMeta[0]!;
  return (
    <Badge tone={meta.tone} dot>
      {meta.label}
    </Badge>
  );
}

export function ObservationStatusBadge({ status }: { status: number }) {
  const meta = observationStatusMeta[status] ?? observationStatusMeta[0]!;
  return (
    <Badge tone={meta.tone} icon={status === 0 ? undefined : <CheckCircle2 className="size-3.5" aria-hidden />} dot={status === 0}>
      {meta.label}
    </Badge>
  );
}

export function RequestStatusBadge({ status }: { status: number }) {
  const meta = requestStatusMeta[status] ?? requestStatusMeta[0]!;
  return (
    <Badge tone={meta.tone} dot>
      {meta.label}
    </Badge>
  );
}

export function PaymentStatusBadge({ status, mode }: { status: string; mode?: string | null }) {
  const meta = paymentStatusMeta[status] ?? { label: status, tone: "neutral" as const };
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <Badge tone={meta.tone} dot>
        {meta.label}
      </Badge>
      {mode === "Simulated" && (
        <Badge tone="pending" icon={<FlaskConical className="size-3.5" aria-hidden />}>
          Simulated
        </Badge>
      )}
    </span>
  );
}

/** Says honestly where a result came from: an AI model, the rule-based fallback, manual entry or demo data. */
export function SourceBadge({ source }: { source: string | null | undefined }) {
  const { label, kind } = sourceLabel(source);
  const icon =
    kind === "ai" ? <Sparkles className="size-3.5" aria-hidden /> : kind === "rules" ? <ListChecks className="size-3.5" aria-hidden /> : kind === "manual" ? <PenLine className="size-3.5" aria-hidden /> : <FlaskConical className="size-3.5" aria-hidden />;
  const tone = kind === "ai" ? "info" : kind === "demo" ? "pending" : "neutral";
  return (
    <Badge tone={tone} icon={icon} className="max-w-full truncate">
      {label}
    </Badge>
  );
}
