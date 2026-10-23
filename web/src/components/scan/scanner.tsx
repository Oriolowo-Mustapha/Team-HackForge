"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AudioLines, Camera, ChevronDown, CloudOff, Info, Mic, PenLine, Sparkles, Type, X } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { ApiError, newIdempotencyKey } from "@/lib/api/client";
import { livestockApi, observationsApi } from "@/lib/api/endpoints";
import { cn } from "@/lib/cn";
import { deleteDraft, getDraft, saveDraft, type ReportDraft } from "@/lib/drafts";
import { useActiveFarm } from "@/lib/hooks/use-active-farm";
import { useOnline } from "@/lib/hooks/use-online";
import { useSystemInfo } from "@/lib/hooks/use-session";
import { qk } from "@/lib/queries";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { Field, Input, Textarea } from "@/components/ui/field";
import { InlineError, Notice } from "@/components/ui/states";
import { AnalysisProgress, type Stage } from "./analysis-progress";
import { CameraCapture } from "./camera-capture";
import { DraftsList } from "./drafts-list";
import { SubjectPicker, type Subject } from "./subject-picker";
import { VoiceRecorder, formatDuration } from "./voice-recorder";

type Mode = "photo" | "voice" | "text";
type Phase = "subject" | "capture" | "sending" | "done";
type Manual = { mortalityCount: string; feed: string; water: string; eggs: string };

const PROMPTS = ["Not eating", "Coughing or sneezing", "Diarrhoea", "Swelling", "Limping", "Found dead", "Fewer eggs", "Weak or sleepy"];
const emptyManual: Manual = { mortalityCount: "", feed: "", water: "", eggs: "" };

export function Scanner({ initial, draftId, showDrafts }: { initial: { group?: string; animal?: string }; draftId?: string; showDrafts?: boolean }) {
  const router = useRouter();
  const client = useQueryClient();
  const online = useOnline();
  const system = useSystemInfo();
  const { farm } = useActiveFarm();
  const farmId = farm?.id ?? "";

  const [id, setId] = useState(() => newIdempotencyKey("draft"));
  const [idempotencyKey, setIdempotencyKey] = useState(() => newIdempotencyKey("obs"));
  const [createdAt] = useState(() => Date.now());
  const [subject, setSubject] = useState<Subject | null>(null);
  const [phase, setPhase] = useState<Phase>("subject");
  const [mode, setMode] = useState<Mode>("photo");
  const [text, setText] = useState("");
  const [image, setImage] = useState<Blob | null>(null);
  const [audio, setAudio] = useState<Blob | null>(null);
  const [audioMs, setAudioMs] = useState<number | null>(null);
  const [manual, setManual] = useState<Manual>(emptyManual);
  const [showNumbers, setShowNumbers] = useState(false);
  const [upload, setUpload] = useState(0);
  const [error, setError] = useState<unknown>(null);
  const [aiDown, setAiDown] = useState<string | null>(null);
  const [manualOnly, setManualOnly] = useState(false);
  const [draftsOpen, setDraftsOpen] = useState(Boolean(showDrafts));
  const abortRef = useRef<AbortController | null>(null);

  // Preselect the subject from ?group= / ?animal=, or restore a saved draft.
  const groups = useQuery({ queryKey: qk.groups(farmId), queryFn: () => livestockApi.groups(farmId), enabled: Boolean(farmId) });
  const animals = useQuery({ queryKey: qk.animals(farmId, {}), queryFn: () => livestockApi.animals(farmId), enabled: Boolean(farmId) });

  // Restore a saved draft (async, from IndexedDB).
  useEffect(() => {
    if (!draftId) return;
    let cancelled = false;
    void getDraft(draftId).then((d) => {
      if (!d || cancelled) return;
      setId(d.id);
      setIdempotencyKey(d.idempotencyKey);
      setSubject({ kind: d.subjectKind, id: d.subjectId, name: d.subjectName, species: null });
      setText(d.text);
      setImage(d.image);
      setAudio(d.audio);
      setAudioMs(d.audioDurationMs);
      if (d.manual) {
        setManual({ mortalityCount: d.manual.mortalityCount?.toString() ?? "", feed: d.manual.feed, water: d.manual.water, eggs: d.manual.eggs });
        setShowNumbers(true);
      }
      setMode(d.image ? "photo" : d.audio ? "voice" : "text");
      setPhase("capture");
    });
    return () => {
      cancelled = true;
    };
  }, [draftId]);

  // Preselect the subject from ?animal= / ?group= (or the only group) once the lists have loaded.
  const [preselected, setPreselected] = useState(Boolean(draftId));
  if (!preselected && groups.data && animals.data) {
    setPreselected(true);
    const animal = initial.animal ? animals.data.find((x) => x.id === initial.animal) : undefined;
    const group = initial.group ? groups.data.find((x) => x.id === initial.group) : undefined;
    const only = !initial.animal && !initial.group && groups.data.length === 1 && animals.data.every((a) => !a.isActive) ? groups.data[0] : undefined;
    if (animal) {
      setSubject({ kind: "animal", id: animal.id, name: animal.name, species: animal.species });
      setPhase("capture");
    } else if (group ?? only) {
      const g = (group ?? only)!;
      setSubject({ kind: "group", id: g.id, name: g.name, species: g.species });
      setPhase("capture");
    }
  }

  const hasContent = Boolean(text.trim() || image || audio);
  const transcription = system.data?.transcriptionConfigured ?? false;
  const vision = system.data?.aiVisionConfigured ?? false;
  const aiText = system.data?.aiTextConfigured ?? false;

  const toDraft = useCallback(
    (lastError: string | null = null): ReportDraft | null => {
      if (!subject || !farmId) return null;
      const deaths = manual.mortalityCount ? Number(manual.mortalityCount) : null;
      const anyManual = manual.mortalityCount || manual.feed || manual.water || manual.eggs;
      return {
        id,
        idempotencyKey,
        farmId,
        subjectKind: subject.kind,
        subjectId: subject.id,
        subjectName: subject.name,
        text,
        image,
        audio,
        audioDurationMs: audioMs,
        manual: anyManual ? { mortalityCount: deaths, feed: manual.feed, water: manual.water, eggs: manual.eggs } : null,
        lastError,
        createdAt,
        updatedAt: Date.now(),
      };
    },
    [subject, farmId, id, idempotencyKey, text, image, audio, audioMs, manual, createdAt],
  );

  // Autosave to this device while the farmer works, so nothing is lost if the tab closes or the signal drops.
  useEffect(() => {
    if (!hasContent || phase === "sending" || phase === "done") return;
    const timer = setTimeout(() => {
      const draft = toDraft();
      if (draft) void saveDraft(draft).catch(() => undefined);
    }, 700);
    return () => clearTimeout(timer);
  }, [hasContent, phase, toDraft]);

  const stages: Stage[] = useMemo(() => {
    const list: Stage[] = [];
    if (audio) list.push({ key: "voice", label: "Transcribing your voice note", detail: transcription ? "Speech-to-text" : undefined });
    if (image) list.push({ key: "photo", label: vision ? "Looking at the photo" : "Attaching the photo", detail: vision ? "Describes what's visible — not a diagnosis" : "Automatic photo review isn't configured on this server" });
    list.push({
      key: "extract",
      label: "Pulling out symptoms and numbers",
      detail: aiText ? "AI-assisted, for you to check" : "Rule-based check (no AI provider configured)",
    });
    list.push({ key: "triage", label: "Suggesting how urgent it looks", detail: "You confirm before anything is saved" });
    return list;
  }, [audio, image, transcription, vision, aiText]);

  async function send(manualEntry = false) {
    if (!subject || !farmId) return;
    if (!hasContent) {
      setError(new Error("Add a photo, a voice note or a few words first."));
      return;
    }
    if (!online) {
      const draft = toDraft("You were offline");
      if (draft) await saveDraft(draft);
      toast("Saved on this device", { description: "Send it from Report a concern when you're back online." });
      return;
    }
    setError(null);
    setAiDown(null);
    setUpload(0);
    setPhase("sending");
    const controller = new AbortController();
    abortRef.current = controller;
    const deaths = manual.mortalityCount ? Number(manual.mortalityCount) : null;
    try {
      const result = await observationsApi.submit(
        {
          farmId,
          livestockAnimalId: subject.kind === "animal" ? subject.id : null,
          livestockCategoryId: subject.kind === "group" ? subject.id : null,
          text: text.trim() || undefined,
          audio: manualEntry ? null : audio,
          image,
          idempotencyKey,
          manualEntry,
          mortalityCount: manualEntry ? deaths : null,
          feedConsumption: manualEntry ? manual.feed || null : null,
          waterConsumption: manualEntry ? manual.water || null : null,
          eggProduction: manualEntry ? manual.eggs || null : null,
        },
        { signal: controller.signal, onUploadProgress: setUpload },
      );
      setPhase("done");
      await deleteDraft(id).catch(() => undefined);
      void client.invalidateQueries({ queryKey: qk.overview(farmId) });
      void client.invalidateQueries({ queryKey: ["observations"] });
      router.replace(`/reports/${result.id}?new=1`);
    } catch (err) {
      setPhase("capture");
      if (err instanceof DOMException && err.name === "AbortError") {
        const draft = toDraft("Stopped before sending finished");
        if (draft) await saveDraft(draft);
        toast("Kept as a draft on this device");
        return;
      }
      const message = err instanceof ApiError ? err.userMessage : "The report could not be sent.";
      const draft = toDraft(message);
      if (draft) await saveDraft(draft).catch(() => undefined);
      if (err instanceof ApiError && err.code === "ai_unavailable") {
        setAiDown(err.detail ?? err.message);
        return;
      }
      setError(err);
    }
  }

  function close() {
    if (hasContent && phase !== "done") {
      const draft = toDraft();
      if (draft) void saveDraft(draft).then(() => toast("Draft saved on this device"));
    }
    router.push("/home");
  }

  // ---- Render ---------------------------------------------------------------------------------

  if (phase === "sending" || phase === "done") {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-ink">
        <AnalysisProgress upload={upload} done={phase === "done"} stages={stages} onCancel={() => abortRef.current?.abort()} />
      </div>
    );
  }

  return (
    <div className="mx-auto flex min-h-dvh max-w-[1100px] flex-col px-4 pb-[calc(24px+env(safe-area-inset-bottom))] pt-[max(12px,env(safe-area-inset-top))] sm:px-6">
      <header className="flex items-center gap-2 py-2">
        <button type="button" onClick={close} aria-label="Close" className="inline-flex size-11 items-center justify-center rounded-2xl bg-paper shadow-soft hover:bg-white">
          <X className="size-5" />
        </button>
        <div className="min-w-0 flex-1 text-center">
          <p className="text-[13px] font-semibold text-muted">Report a concern</p>
          {subject && phase === "capture" ? (
            <button type="button" onClick={() => setPhase("subject")} className="mx-auto inline-flex max-w-full items-center gap-1 rounded-xl px-2 py-0.5 text-[17px] font-semibold hover:bg-paper">
              <span className="truncate">{subject.name}</span>
              <ChevronDown className="size-4 shrink-0 text-muted" aria-hidden />
              <span className="sr-only">Change who this report is about</span>
            </button>
          ) : (
            <p className="text-[17px] font-semibold">Who is it about?</p>
          )}
        </div>
        <button type="button" onClick={() => setDraftsOpen(true)} aria-label="Unsent reports on this device" className="inline-flex size-11 items-center justify-center rounded-2xl bg-paper shadow-soft hover:bg-white">
          <CloudOff className="size-5" />
        </button>
      </header>

      {phase === "subject" ? (
        <div className="mx-auto w-full max-w-2xl flex-1 pt-4">
          {farmId && (
            <SubjectPicker
              farmId={farmId}
              value={subject}
              onChange={(s) => {
                setSubject(s);
                setPhase("capture");
              }}
            />
          )}
        </div>
      ) : (
        <div className="grid flex-1 gap-5 pt-3 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] lg:gap-8">
          {/* Scanner panel */}
          <section aria-label="Capture" className="flex flex-col rounded-[34px] bg-ink p-3 text-cream shadow-lift">
            <div className="h-[min(58dvh,520px)] min-h-[340px] lg:h-[560px]">
              {mode === "photo" ? (
                <CameraCapture image={image} onImage={setImage} />
              ) : mode === "voice" ? (
                transcription ? (
                  <VoiceRecorder
                    audio={audio}
                    durationMs={audioMs}
                    onAudio={(blob, ms) => {
                      setAudio(blob);
                      setAudioMs(ms);
                    }}
                  />
                ) : (
                  <div className="flex h-full flex-col items-center justify-center gap-4 px-8 text-center">
                    <span className="flex size-16 items-center justify-center rounded-full bg-cream/8">
                      <AudioLines className="size-7" aria-hidden />
                    </span>
                    <p className="text-lg font-semibold">Voice notes aren&apos;t available here</p>
                    <p className="max-w-xs text-sm leading-relaxed text-cream/70">
                      This server has no speech-to-text service configured, so a recording couldn&apos;t be turned into a report. Type what you would have said instead.
                    </p>
                    <button type="button" onClick={() => setMode("text")} className="mt-2 inline-flex h-12 items-center gap-2 rounded-full bg-cream px-5 text-sm font-semibold text-ink">
                      <Type className="size-4" aria-hidden /> Type instead
                    </button>
                  </div>
                )
              ) : (
                <div className="flex h-full flex-col p-3">
                  <label htmlFor="report-text" className="px-1 pb-2 text-sm font-semibold text-cream/80">
                    Describe what you see
                  </label>
                  <textarea
                    id="report-text"
                    value={text}
                    onChange={(e) => setText(e.target.value.slice(0, 2000))}
                    placeholder="e.g. Three hens found dead this morning. Others are sleepy with ruffled feathers and eating less since yesterday."
                    className="min-h-0 flex-1 resize-none rounded-[22px] bg-ink-2 p-4 text-[16px] leading-relaxed text-cream outline-none ring-1 ring-cream/10 placeholder:text-cream/35 focus:ring-2 focus:ring-terracotta"
                  />
                  <div className="mt-3 flex flex-wrap gap-1.5">
                    {PROMPTS.map((p) => (
                      <button
                        key={p}
                        type="button"
                        onClick={() => setText((t) => (t.trim() ? `${t.trim().replace(/[.,]?$/, ",")} ${p.toLowerCase()}` : p))}
                        className="rounded-full bg-cream/8 px-3 py-1.5 text-[13px] font-semibold text-cream/85 ring-1 ring-cream/10 hover:bg-cream/14"
                      >
                        + {p}
                      </button>
                    ))}
                  </div>
                  <p className="mt-2 px-1 text-right text-xs text-cream/45 tabular">{text.length}/2000</p>
                </div>
              )}
            </div>

            <div role="tablist" aria-label="How do you want to report?" className="mx-auto mt-3 flex w-full max-w-sm gap-1 rounded-full bg-cream/8 p-1">
              {(
                [
                  { key: "photo", label: "Photo", icon: Camera, has: Boolean(image) },
                  { key: "voice", label: "Voice", icon: Mic, has: Boolean(audio) },
                  { key: "text", label: "Text", icon: Type, has: Boolean(text.trim()) },
                ] as const
              ).map((tab) => (
                <button
                  key={tab.key}
                  type="button"
                  role="tab"
                  aria-selected={mode === tab.key}
                  onClick={() => setMode(tab.key)}
                  className={cn(
                    "relative inline-flex h-11 flex-1 items-center justify-center gap-2 rounded-full text-sm font-semibold transition-colors",
                    mode === tab.key ? "bg-terracotta text-white" : "text-cream/70 hover:text-cream",
                  )}
                >
                  <tab.icon className="size-4" aria-hidden />
                  {tab.label}
                  {tab.has && <span className="absolute right-3 top-2 size-2 rounded-full bg-green" aria-label="added" />}
                </button>
              ))}
            </div>
          </section>

          {/* Report tray */}
          <section aria-label="Your report" className="flex flex-col gap-4">
            <div>
              <h1 className="text-[24px] font-semibold tracking-[-0.02em]">Your report</h1>
              <p className="mt-1 text-sm text-muted">Combine a photo, a voice note and a few words — the more detail, the better the summary.</p>
            </div>

            <ul className="grid grid-cols-3 gap-2" aria-label="Attached">
              {[
                { key: "photo", icon: Camera, label: "Photo", value: image ? "Added" : "None", on: Boolean(image) },
                { key: "voice", icon: Mic, label: "Voice", value: audio ? formatDuration(audioMs) : "None", on: Boolean(audio) },
                { key: "text", icon: Type, label: "Note", value: text.trim() ? `${text.trim().split(/\s+/).length} words` : "None", on: Boolean(text.trim()) },
              ].map((c) => (
                <li key={c.key}>
                  <button
                    type="button"
                    onClick={() => setMode(c.key as Mode)}
                    className={cn("flex w-full flex-col items-start gap-2 rounded-[20px] p-3 text-left ring-1", c.on ? "bg-paper ring-ink/80" : "bg-paper/60 ring-line")}
                  >
                    <c.icon className={cn("size-5", c.on ? "text-terracotta-ink" : "text-faint")} aria-hidden />
                    <span>
                      <span className="block text-sm font-semibold">{c.label}</span>
                      <span className="block text-xs text-muted">{c.value}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>

            {mode !== "text" && (
              <Field label="Add a note" optional hint="How many animals, since when, anything you've already tried.">
                <Textarea value={text} onChange={(e) => setText(e.target.value.slice(0, 2000))} className="min-h-24" placeholder="e.g. Started yesterday evening, about 10 birds." />
              </Field>
            )}

            <div className="rounded-[22px] bg-paper ring-1 ring-line/60">
              <button type="button" onClick={() => setShowNumbers((v) => !v)} aria-expanded={showNumbers} className="flex w-full items-center justify-between px-4 py-3 text-left">
                <span className="text-sm font-semibold">Flock numbers today</span>
                <span className="flex items-center gap-1 text-xs text-muted">
                  Optional <ChevronDown className={cn("size-4 transition-transform", showNumbers && "rotate-180")} aria-hidden />
                </span>
              </button>
              {showNumbers && (
                <div className="grid grid-cols-2 gap-3 border-t border-line px-4 pb-4 pt-3">
                  <Field label="Deaths">
                    <Input inputMode="numeric" value={manual.mortalityCount} onChange={(e) => setManual((m) => ({ ...m, mortalityCount: e.target.value.replace(/[^\d]/g, "").slice(0, 5) }))} placeholder="0" />
                  </Field>
                  <Field label="Eggs collected">
                    <Input value={manual.eggs} onChange={(e) => setManual((m) => ({ ...m, eggs: e.target.value.slice(0, 100) }))} placeholder="about 400" />
                  </Field>
                  <Field label="Feed">
                    <Input value={manual.feed} onChange={(e) => setManual((m) => ({ ...m, feed: e.target.value.slice(0, 100) }))} placeholder="normal / reduced" />
                  </Field>
                  <Field label="Water">
                    <Input value={manual.water} onChange={(e) => setManual((m) => ({ ...m, water: e.target.value.slice(0, 100) }))} placeholder="normal / more" />
                  </Field>
                  <p className="col-span-2 text-xs text-faint">You can also correct numbers on the next screen before saving.</p>
                </div>
              )}
            </div>

            {system.data && (!aiText || !vision) && (
              <Notice tone="demo" icon={<Info />}>
                {aiText
                  ? "Photos are attached to the report but not analysed automatically on this server."
                  : "No AI provider is configured on this server. Your words are checked with transparent rules instead, and photos are attached without automatic review."}
              </Notice>
            )}

            {aiDown && (
              <div className="space-y-3 rounded-[24px] bg-terracotta-soft p-4 text-terracotta-deep">
                <p className="font-semibold">Automatic analysis isn&apos;t available right now</p>
                <p className="text-sm leading-relaxed">{aiDown} Your report is saved on this device.</p>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Button
                    variant="primary"
                    icon={<PenLine className="size-4" />}
                    onClick={() => {
                      setShowNumbers(true);
                      setManualOnly(true);
                      if (!text.trim()) setMode("text");
                      else void send(true);
                    }}
                  >
                    {text.trim() ? "Send without analysis" : "Describe it, then send"}
                  </Button>
                  <Button variant="secondary" onClick={() => void send(false)}>
                    Try analysis again
                  </Button>
                </div>
                <p className="text-xs">Sending without analysis saves your words and numbers as you entered them. You can still open a case or ask a vet.</p>
              </div>
            )}

            <InlineError error={error} />
            {!online && (
              <Notice tone="warning" icon={<CloudOff />}>
                You&apos;re offline. Your report is saved on this device — send it when you&apos;re connected.
              </Notice>
            )}

            <div className="mt-auto space-y-2 pt-2">
              <Button size="lg" variant="accent" block disabled={!hasContent} onClick={() => void send(manualOnly)} icon={manualOnly ? <PenLine className="size-5" /> : <Sparkles className="size-5" />}>
                {!online ? "Save for later" : manualOnly ? "Send without analysis" : "Analyse report"}
              </Button>
              <p className="text-center text-xs leading-relaxed text-faint">
                FarmHand suggests symptoms and urgency for you to check. It does not diagnose — a vet does.
              </p>
            </div>
          </section>
        </div>
      )}

      <Modal open={draftsOpen} onOpenChange={setDraftsOpen} title="Unsent reports" description="Saved on this device only. They're erased when you sign out.">
        {farmId && <DraftsList farmId={farmId} />}
        <div className="mt-4 text-center">
          <Link href="/reports" className="text-sm font-semibold text-muted hover:text-ink">
            See sent reports
          </Link>
        </div>
      </Modal>
    </div>
  );
}
