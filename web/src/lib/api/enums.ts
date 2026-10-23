/**
 * Backend enums are serialized as integers. This module is the single place that maps them to
 * labels and visual tones, so screens never invent their own status vocabulary.
 */

export type Tone = "healthy" | "pending" | "concerning" | "urgent" | "critical" | "confirmed" | "failed" | "neutral" | "info";

export const Severity = { Low: 0, Medium: 1, High: 2, Critical: 3 } as const;
export type SeverityValue = (typeof Severity)[keyof typeof Severity];

export const severityMeta: Record<number, { label: string; tone: Tone; description: string }> = {
  0: { label: "Low", tone: "healthy", description: "Keep observing" },
  1: { label: "Medium", tone: "concerning", description: "Watch closely" },
  2: { label: "High", tone: "urgent", description: "Get a vet's opinion" },
  3: { label: "Critical", tone: "critical", description: "Act today" },
};

export const IncidentStatus = { Open: 0, Acknowledged: 1, UnderMonitoring: 2, FollowUpRequired: 3, Resolved: 4, Reopened: 5 } as const;

export const incidentStatusMeta: Record<number, { label: string; tone: Tone; verb: string }> = {
  0: { label: "Open", tone: "concerning", verb: "Reopen" },
  1: { label: "Acknowledged", tone: "pending", verb: "Acknowledge" },
  2: { label: "Monitoring", tone: "info", verb: "Monitor" },
  3: { label: "Follow-up due", tone: "pending", verb: "Schedule follow-up" },
  4: { label: "Resolved", tone: "healthy", verb: "Resolve" },
  5: { label: "Reopened", tone: "concerning", verb: "Reopen" },
};

export const ObservationStatus = { Pending: 0, Confirmed: 1, Corrected: 2 } as const;

export const observationStatusMeta: Record<number, { label: string; tone: Tone }> = {
  0: { label: "Needs review", tone: "pending" },
  1: { label: "Confirmed", tone: "confirmed" },
  2: { label: "Confirmed with edits", tone: "confirmed" },
};

export const RequestStatus = { Pending: 0, Responded: 1, Completed: 2, Cancelled: 3, Declined: 4 } as const;

export const requestStatusMeta: Record<number, { label: string; tone: Tone }> = {
  0: { label: "Waiting for a vet", tone: "pending" },
  1: { label: "Vet assigned", tone: "info" },
  2: { label: "Completed", tone: "healthy" },
  3: { label: "Cancelled", tone: "neutral" },
  4: { label: "Declined", tone: "failed" },
};

export const RequestType = { RemoteReview: 0, SharedVisit: 1, CooperativePlan: 2, ClinicVisit: 3 } as const;

export const requestTypeMeta: Record<number, { label: string; description: string }> = {
  0: { label: "Remote review", description: "A vet reviews your report, photos and history remotely and advises you." },
  1: { label: "Farm visit", description: "A vet visits the farm to examine the animals." },
  2: { label: "Co-op plan", description: "A vet reviews the case as part of a cooperative health plan." },
  3: { label: "Clinic visit", description: "You bring the animal to the vet's clinic." },
};

export const Outcome = { Improving: 0, Worsening: 1, Resolved: 2 } as const;

export const outcomeMeta: Record<number, { label: string; tone: Tone }> = {
  0: { label: "Improving", tone: "healthy" },
  1: { label: "Worsening", tone: "urgent" },
  2: { label: "Resolved", tone: "healthy" },
};

export const paymentStatusMeta: Record<string, { label: string; tone: Tone }> = {
  Pending: { label: "Awaiting payment", tone: "pending" },
  Completed: { label: "Paid", tone: "healthy" },
  Failed: { label: "Failed", tone: "failed" },
  Cancelled: { label: "Cancelled", tone: "neutral" },
  NotRequired: { label: "No fee", tone: "neutral" },
  Unpaid: { label: "Unpaid", tone: "pending" },
  Paid: { label: "Paid", tone: "healthy" },
};

/** Human label for where structured fields or an assessment came from. */
export function sourceLabel(source: string | null | undefined): { label: string; kind: "ai" | "rules" | "manual" | "demo" | "unknown" } {
  if (!source) return { label: "Unknown source", kind: "unknown" };
  if (source === "demo-seed") return { label: "Demo record", kind: "demo" };
  if (source === "rules") return { label: "Rule-based check (no AI)", kind: "rules" };
  if (source === "manual") return { label: "Entered manually", kind: "manual" };
  const [provider, model] = source.split(":");
  if (provider === "gemini") return { label: `AI · Google Gemini${model ? ` (${model})` : ""}`, kind: "ai" };
  if (provider === "groq") return { label: `AI · Groq${model ? ` (${model})` : ""}`, kind: "ai" };
  if (source.toLowerCase().startsWith("gemini/groq")) return { label: "AI (legacy)", kind: "ai" };
  return { label: source, kind: "unknown" };
}
