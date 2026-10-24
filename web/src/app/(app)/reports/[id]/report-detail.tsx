"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowRight, Camera, CheckCircle2, ClipboardPlus, FileAudio, HelpCircle, Stethoscope, Trash2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { incidentsApi, observationsApi } from "@/lib/api/endpoints";
import type { ConfirmResult, ObservationDetail } from "@/lib/api/schemas";
import { severityMeta } from "@/lib/api/enums";
import { formatDateTime, formatRelative } from "@/lib/format";
import { languageName } from "@/lib/language";
import { mediaUrl } from "@/lib/images";
import { qk } from "@/lib/queries";
import { AssessmentCard, severityFromName } from "@/components/domain/assessment-card";
import { ListenButton } from "@/components/domain/listen-button";
import { ObservationStatusBadge, SourceBadge } from "@/components/domain/badges";
import { Badge } from "@/components/ui/badge";
import { Button, ButtonLink } from "@/components/ui/button";
import { Card, CardTitle } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ui/dialog";
import { ChoiceChips, Field, Input, Textarea } from "@/components/ui/field";
import { PageHeader } from "@/components/ui/page-header";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorState, InlineError, Notice } from "@/components/ui/states";

export function ReportDetail({ id, isNew }: { id: string; isNew: boolean }) {
  const report = useQuery({ queryKey: qk.observation(id), queryFn: () => observationsApi.get(id) });
  const [result, setResult] = useState<ConfirmResult | null>(null);

  if (report.isPending)
    return (
      <div className="grid gap-6 lg:grid-cols-2">
        <Skeleton className="h-96 rounded-[28px]" />
        <Skeleton className="h-96 rounded-[28px]" />
      </div>
    );
  if (report.isError) return <ErrorState error={report.error} onRetry={() => void report.refetch()} />;

  const o = report.data;
  const pending = o.confirmationStatus === 0 && !result;

  return (
    <div>
      <PageHeader
        back="/reports"
        eyebrow={`Health report · ${formatRelative(o.recordedAt)}`}
        title={o.subjectName ?? "Farm report"}
        subtitle={pending ? "Check what FarmHand picked up, correct anything that's wrong, then confirm." : undefined}
      />
      {pending && isNew && o.reused && (
        <Notice className="mb-5" tone="info">
          This report was already received — you&apos;re seeing the original, so nothing was duplicated.
        </Notice>
      )}
      <div className="grid gap-6 lg:grid-cols-12">
        <div className="space-y-5 lg:col-span-5">
          <Evidence o={o} />
        </div>
        <div className="space-y-5 lg:col-span-7">
          {result ? <ConfirmOutcome o={o} result={result} /> : pending ? <ReviewForm o={o} onConfirmed={setResult} /> : <ConfirmedSummary o={o} />}
        </div>
      </div>
    </div>
  );
}

function Evidence({ o }: { o: ObservationDetail }) {
  const photo = mediaUrl(o.imageUrl);
  return (
    <>
      <div className="flex flex-wrap gap-2">
        <ObservationStatusBadge status={o.confirmationStatus} />
        <SourceBadge source={o.analysisSource} />
      </div>
      {photo && (
        <figure className="overflow-hidden rounded-[28px] bg-paper ring-1 ring-line/60">
          {/* eslint-disable-next-line @next/next/no-img-element -- report photo served through the authenticated BFF */}
          <img src={photo} alt="Photo attached to this report" className="max-h-[420px] w-full object-cover" />
          <figcaption className="space-y-2 p-4 text-sm">
            {o.vision?.available ? (
              <>
                <p className="text-xs font-semibold uppercase tracking-[0.08em] text-faint">What the photo shows</p>
                <p className="leading-relaxed">{o.vision.description}</p>
                {o.vision.detectedIssues.length > 0 && (
                  <div className="flex flex-wrap gap-1.5">
                    {o.vision.detectedIssues.map((issue) => (
                      <Badge key={issue} tone="neutral">
                        {issue}
                      </Badge>
                    ))}
                  </div>
                )}
                <p className="text-xs text-faint">AI image description — visible signs only, not a diagnosis.</p>
              </>
            ) : (
              <p className="flex items-center gap-2 text-muted">
                <Camera className="size-4" aria-hidden /> Photo attached. It was not analysed automatically.
              </p>
            )}
          </figcaption>
        </figure>
      )}
      {o.rawTranscript && (
        <Card>
          <CardTitle action={<FileAudio className="size-4 text-faint" aria-hidden />}>Voice note transcript</CardTitle>
          <blockquote className="border-l-[3px] border-beige pl-4 text-[15px] italic leading-relaxed text-ink-3">{o.rawTranscript}</blockquote>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
            {languageName(o.detectedLanguage) && (
              <span className="rounded-full bg-sand px-3 py-1 text-xs font-semibold text-ink-3">
                Reported in {languageName(o.detectedLanguage)}
              </span>
            )}
            <ListenButton text={o.rawTranscript} label="Listen" targetLanguage={o.detectedLanguage ?? undefined} className="mt-3" />
          </div>
        </Card>
      )}
      <Card>
        <CardTitle>Recorded</CardTitle>
        <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
          <div>
            <dt className="text-muted">When</dt>
            <dd className="font-semibold">{formatDateTime(o.recordedAt)}</dd>
          </div>
          <div>
            <dt className="text-muted">Sent as</dt>
            <dd className="font-semibold capitalize">{o.inputChannels?.replace(/,/g, ", ") ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-muted">About</dt>
            <dd className="font-semibold">{o.subjectName ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-muted">Species</dt>
            <dd className="font-semibold">{o.subjectSpecies ?? "—"}</dd>
          </div>
          {languageName(o.detectedLanguage) && (
            <div>
              <dt className="text-muted">Language</dt>
              <dd className="font-semibold">{languageName(o.detectedLanguage)}</dd>
            </div>
          )}
        </dl>
      </Card>
    </>
  );
}

function ReviewForm({ o, onConfirmed }: { o: ObservationDetail; onConfirmed: (r: ConfirmResult) => void }) {
  const client = useQueryClient();
  const router = useRouter();
  const [notes, setNotes] = useState(o.farmerNotes ?? "");
  const [numbers, setNumbers] = useState({
    deaths: String(o.mortalityCount ?? 0),
    feed: o.feedConsumption ?? "",
    water: o.waterConsumption ?? "",
    eggs: o.eggProduction ?? "",
  });
  const [urgency, setUrgency] = useState<string>("suggest");
  const [reason, setReason] = useState("");
  const [discarding, setDiscarding] = useState(false);

  const confirm = useMutation({
    mutationFn: () =>
      observationsApi.confirm(o.id, {
        farmerNotes: notes.trim() || null,
        overrideSeverity: urgency === "suggest" ? null : Number(urgency),
        overrideReason: urgency === "suggest" ? null : reason.trim() || null,
        mortalityCount: numbers.deaths === "" ? null : Number(numbers.deaths),
        feedConsumption: numbers.feed.trim() || null,
        waterConsumption: numbers.water.trim() || null,
        eggProduction: numbers.eggs.trim() || null,
      }),
    onSuccess: async (result) => {
      onConfirmed(result);
      window.scrollTo({ top: 0, behavior: "smooth" });
      await Promise.all([
        client.invalidateQueries({ queryKey: qk.observation(o.id) }),
        client.invalidateQueries({ queryKey: qk.overview(o.farmId) }),
        client.invalidateQueries({ queryKey: ["observations"] }),
        client.invalidateQueries({ queryKey: ["incidents"] }),
      ]);
    },
  });

  const discard = useMutation({
    mutationFn: () => observationsApi.discard(o.id),
    onSuccess: async () => {
      toast.success("Report discarded");
      await client.invalidateQueries({ queryKey: qk.overview(o.farmId) });
      router.replace("/reports");
    },
  });

  return (
    <div className="space-y-5">
      <Card>
        <CardTitle action={<SourceBadge source={o.analysisSource} />}>Symptoms picked up</CardTitle>
        {o.symptoms.length ? (
          <ul className="flex flex-wrap gap-2">
            {o.symptoms.map((s) => (
              <li key={s} className="rounded-full bg-sand px-3.5 py-1.5 text-sm font-semibold">
                {s}
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted">No specific symptoms were recognised. Add detail to your notes if something is wrong.</p>
        )}
        {o.missingInformation.length > 0 && (
          <div className="mt-4 rounded-2xl bg-olive-soft p-4 text-olive-ink">
            <p className="flex items-center gap-2 text-sm font-semibold">
              <HelpCircle className="size-4" aria-hidden /> Answering these helps
            </p>
            <ul className="mt-2 list-disc space-y-1 pl-5 text-sm">
              {o.missingInformation.map((q) => (
                <li key={q}>{q}</li>
              ))}
            </ul>
          </div>
        )}
      </Card>

      <Card>
        <CardTitle>Check the details</CardTitle>
        <div className="space-y-4">
          <Field label="Notes" hint="Correct anything that was misheard and add answers to the questions above.">
            <Textarea value={notes} onChange={(e) => setNotes(e.target.value.slice(0, 1000))} className="min-h-28" />
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Deaths">
              <Input inputMode="numeric" value={numbers.deaths} onChange={(e) => setNumbers((n) => ({ ...n, deaths: e.target.value.replace(/[^\d]/g, "").slice(0, 5) }))} />
            </Field>
            <Field label="Eggs" optional>
              <Input value={numbers.eggs} onChange={(e) => setNumbers((n) => ({ ...n, eggs: e.target.value.slice(0, 100) }))} placeholder="about 400" />
            </Field>
            <Field label="Feed" optional>
              <Input value={numbers.feed} onChange={(e) => setNumbers((n) => ({ ...n, feed: e.target.value.slice(0, 100) }))} placeholder="normal / reduced" />
            </Field>
            <Field label="Water" optional>
              <Input value={numbers.water} onChange={(e) => setNumbers((n) => ({ ...n, water: e.target.value.slice(0, 100) }))} placeholder="normal / more" />
            </Field>
          </div>
        </div>
      </Card>

      <Card>
        <CardTitle>How urgent is it?</CardTitle>
        <p className="-mt-1 mb-3 text-sm text-muted">FarmHand suggests an urgency when you confirm. If you already know, choose it — High or Critical opens a case.</p>
        <ChoiceChips
          label="Urgency"
          value={urgency}
          onChange={setUrgency}
          options={[{ value: "suggest", label: "Let FarmHand suggest" }, ...[0, 1, 2, 3].map((s) => ({ value: String(s), label: severityMeta[s]!.label, description: severityMeta[s]!.description }))]}
        />
        {urgency !== "suggest" && (
          <Field label="Why?" optional className="mt-4">
            <Input value={reason} onChange={(e) => setReason(e.target.value.slice(0, 1000))} placeholder="e.g. Same signs as the outbreak last year" />
          </Field>
        )}
      </Card>

      <InlineError error={confirm.error} />
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
        <Button variant="ghost" icon={<Trash2 className="size-4" />} onClick={() => setDiscarding(true)}>
          Discard report
        </Button>
        <Button size="lg" onClick={() => confirm.mutate()} loading={confirm.isPending} icon={<CheckCircle2 className="size-5" />}>
          Confirm report
        </Button>
      </div>
      <ConfirmDialog
        open={discarding}
        onOpenChange={setDiscarding}
        title="Discard this report?"
        description="It will be removed and won't count towards your farm's records. This can't be undone."
        confirmLabel="Discard"
        loading={discard.isPending}
        onConfirm={() => discard.mutate()}
      />
    </div>
  );
}

function ConfirmOutcome({ o, result }: { o: ObservationDetail; result: ConfirmResult }) {
  const router = useRouter();
  const final = severityFromName(result.finalSeverity) ?? 0;
  const suggested = severityFromName(result.aiSeverity);
  const track = useMutation({
    mutationFn: () =>
      incidentsApi.create({
        farmId: o.farmId,
        livestockAnimalId: o.livestockAnimalId,
        livestockCategoryId: o.livestockAnimalId ? null : o.livestockCategoryId,
        incidentType: o.symptoms[0] ? `${o.symptoms[0]} — ${o.subjectName ?? "farm"}` : `Health concern — ${o.subjectName ?? "farm"}`,
        severity: final,
        triggeringObservations: o.id,
        evidenceSummary: result.aiDiagnosisSummary ?? o.farmerNotes ?? "Opened from a confirmed health report.",
      }),
    onSuccess: ({ id }) => router.push(`/cases/${id}`),
  });

  return (
    <div className="space-y-5">
      <Notice tone="success" icon={<CheckCircle2 />} title="Report confirmed">
        It&apos;s now part of {o.subjectName ?? "your farm"}&apos;s health record.
      </Notice>
      <AssessmentCard
        assessment={{
          severity: final,
          summary: result.aiDiagnosisSummary ?? "",
          farmerActions: result.aiFarmerActions ?? [],
          recommendedAction: result.aiRecommendedAction ?? "",
          requiresVetConsultation: final >= 2,
          source: result.assessmentSource,
          isAi: result.assessmentIsAi,
          assessedAt: new Date().toISOString(),
        }}
        overriddenFrom={result.severityOverridden ? suggested : null}
        audioUrl={result.responseAudioUrl}
        language={result.responseLanguage ?? o.detectedLanguage}
        autoPlay={Boolean(result.responseAudioUrl && o.inputChannels?.includes("voice"))}
      />
      {result.incidentAutoCreated && result.autoCreatedIncidentId ? (
        <div className="rounded-[28px] bg-ink p-6 text-cream">
          <p className="text-xs font-semibold uppercase tracking-[0.08em] text-terracotta-soft">Case opened</p>
          <p className="mt-1 text-lg font-semibold">Because this looks {severityMeta[final]?.label.toLowerCase()}, FarmHand opened a case to track it.</p>
          <div className="mt-4 flex flex-col gap-2 sm:flex-row">
            <ButtonLink href={`/cases/${result.autoCreatedIncidentId}?request=1`} variant="accent" icon={<Stethoscope className="size-4" />}>
              Ask a vet
            </ButtonLink>
            <ButtonLink href={`/cases/${result.autoCreatedIncidentId}`} variant="inverse" trailingIcon={<ArrowRight className="size-4" />}>
              Open the case
            </ButtonLink>
          </div>
        </div>
      ) : (
        <Card>
          <p className="font-semibold">No case opened</p>
          <p className="mt-1 text-sm text-muted">Low and medium urgency reports are kept in the record without opening a case. If you want to track actions and follow-ups, open one yourself.</p>
          <InlineError error={track.error} className="mt-3" />
          <div className="mt-4 flex flex-col gap-2 sm:flex-row">
            <Button variant="secondary" icon={<ClipboardPlus className="size-4" />} loading={track.isPending} onClick={() => track.mutate()}>
              Track as a case
            </Button>
            <ButtonLink href="/home">Done</ButtonLink>
          </div>
        </Card>
      )}
    </div>
  );
}

function ConfirmedSummary({ o }: { o: ObservationDetail }) {
  return (
    <div className="space-y-5">
      {o.assessment ? <AssessmentCard assessment={o.assessment} /> : <Notice>No urgency assessment is stored for this report.</Notice>}
      {(o.symptoms.length > 0 || o.farmerNotes) && (
        <Card>
          <CardTitle>Details</CardTitle>
          {o.symptoms.length > 0 && (
            <ul className="mb-3 flex flex-wrap gap-2">
              {o.symptoms.map((s) => (
                <li key={s} className="rounded-full bg-sand px-3 py-1 text-sm font-semibold">
                  {s}
                </li>
              ))}
            </ul>
          )}
          {o.farmerNotes && <p className="text-[15px] leading-relaxed">{o.farmerNotes}</p>}
          <ListenButton
            text={[
              o.farmerNotes,
              o.assessment?.summary,
              ...(o.assessment?.farmerActions?.length ? ["What you can do now: " + o.assessment.farmerActions.join("; ")] : []),
              o.assessment?.recommendedAction,
            ].filter(Boolean).join(". ")}
            label="Listen to summary"
            targetLanguage={o.detectedLanguage ?? undefined}
            className="mt-2"
          />
          <dl className="mt-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
            {[
              ["Deaths", String(o.mortalityCount)],
              ["Eggs", o.eggProduction],
              ["Feed", o.feedConsumption],
              ["Water", o.waterConsumption],
            ].map(([k, v]) => (
              <div key={k} className="rounded-2xl bg-sand px-3 py-2">
                <dt className="text-xs text-muted">{k}</dt>
                <dd className="font-semibold">{v || "—"}</dd>
              </div>
            ))}
          </dl>
        </Card>
      )}
      {o.incidentId && (
        <Link href={`/cases/${o.incidentId}`} className="flex items-center justify-between rounded-[24px] bg-ink px-5 py-4 text-cream hover:bg-ink-2">
          <span className="font-semibold">Open the related case</span>
          <ArrowRight className="size-5" aria-hidden />
        </Link>
      )}
    </div>
  );
}
