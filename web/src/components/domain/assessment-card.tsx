import { AlertCircle, AlertTriangle, ShieldCheck, Siren, Stethoscope } from "lucide-react";
import type { Assessment } from "@/lib/api/schemas";
import { severityMeta } from "@/lib/api/enums";
import { cn } from "@/lib/cn";
import { formatDateTime } from "@/lib/format";
import { SourceBadge } from "./badges";
import { ListenButton } from "./listen-button";

const icons = [ShieldCheck, AlertCircle, AlertTriangle, Siren];
const panel = ["bg-green-soft text-green-ink", "bg-terracotta-soft text-terracotta-deep", "bg-terracotta-ink text-white", "bg-red-ink text-white"];

export const severityFromName = (name: string | null | undefined): number | null =>
  name ? ({ Low: 0, Medium: 1, High: 2, Critical: 3 } as Record<string, number>)[name] ?? null : null;

/** The triage outcome: urgency, what it might mean and what to do — always labelled with its source. */
export function AssessmentCard({
  assessment,
  overriddenFrom,
  className,
  audioUrl,
  language,
  autoPlay = false,
}: {
  assessment: Assessment;
  overriddenFrom?: number | null;
  className?: string;
  /** Ready-made spoken reply (set for voice reports after confirmation). */
  audioUrl?: string | null;
  /** Language code of the reply, used to pick the matching voice. */
  language?: string | null;
  /** Start playing the spoken reply automatically. */
  autoPlay?: boolean;
}) {
  const meta = severityMeta[assessment.severity] ?? severityMeta[0]!;
  const Icon = icons[assessment.severity] ?? ShieldCheck;
  return (
    <div className={cn("overflow-hidden rounded-[28px] bg-paper ring-1 ring-line/60", className)}>
      <div className={cn("flex items-center gap-3 px-5 py-4", panel[assessment.severity])}>
        <Icon className="size-7 shrink-0" aria-hidden />
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.08em] opacity-80">Suggested urgency</p>
          <p className="text-[22px] font-semibold leading-tight tracking-[-0.02em]">
            {meta.label} · {meta.description}
          </p>
        </div>
      </div>
      <div className="space-y-4 p-5">
        {overriddenFrom != null && overriddenFrom !== assessment.severity && (
          <p className="text-sm text-muted">You changed this from {severityMeta[overriddenFrom]?.label ?? "the suggestion"}.</p>
        )}
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.08em] text-faint">What it could mean</p>
          <p className="mt-1 text-[15px] leading-relaxed">{assessment.summary}</p>
        </div>
        {assessment.farmerActions.length > 0 && (
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.08em] text-faint">What you can do now</p>
            <ul className="mt-1 space-y-1.5">
              {assessment.farmerActions.map((action, i) => (
                <li key={i} className="flex items-start gap-2 text-[15px] leading-relaxed">
                  <span className="mt-2 size-1.5 shrink-0 rounded-full bg-terracotta" aria-hidden />
                  {action}
                </li>
              ))}
            </ul>
          </div>
        )}
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.08em] text-faint">Suggested next steps</p>
          <p className="mt-1 text-[15px] leading-relaxed">{assessment.recommendedAction}</p>
        </div>
        {assessment.requiresVetConsultation && (
          <p className="flex items-start gap-2 rounded-2xl bg-sand px-3 py-2.5 text-sm font-medium">
            <Stethoscope className="mt-0.5 size-4 shrink-0" aria-hidden /> A veterinarian should examine this.
          </p>
        )}
        <div className="flex flex-wrap items-center gap-2 border-t border-line pt-3">
          <SourceBadge source={assessment.source} />
          <span className="text-xs text-faint">{formatDateTime(assessment.assessedAt)}</span>
          <ListenButton
            text={[
              assessment.summary,
              ...assessment.farmerActions,
              assessment.recommendedAction,
            ].filter(Boolean).join(". ")}
            label="Listen"
            targetLanguage={language ?? undefined}
            src={audioUrl}
            autoPlay={autoPlay}
            playLabel="Play spoken reply"
            className={autoPlay && audioUrl ? "ml-auto bg-sand rounded-full px-3 py-1.5 -my-1.5" : "ml-auto"}
          />
        </div>
        <p className="text-xs leading-relaxed text-faint">This is a triage suggestion, not a diagnosis. Only a veterinarian can diagnose disease.</p>
      </div>
    </div>
  );
}
