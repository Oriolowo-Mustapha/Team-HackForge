"use client";

import { Mic, RotateCcw, Square, Trash2 } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { useObjectUrl } from "@/lib/hooks/use-object-url";

const MAX_MS = 3 * 60 * 1000;
const BARS = 28;

function pickMimeType(): string | undefined {
  if (typeof MediaRecorder === "undefined") return undefined;
  return ["audio/webm;codecs=opus", "audio/webm", "audio/mp4", "audio/ogg;codecs=opus"].find((t) => MediaRecorder.isTypeSupported(t));
}

export function formatDuration(ms: number | null | undefined) {
  const total = Math.round((ms ?? 0) / 1000);
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, "0")}`;
}

/** Records a voice note with a live level meter. Farmers can speak in their own words and language. */
export function VoiceRecorder({
  audio,
  durationMs,
  onAudio,
}: {
  audio: Blob | null;
  durationMs: number | null;
  onAudio: (blob: Blob | null, durationMs: number | null) => void;
}) {
  const [state, setState] = useState<"idle" | "recording" | "denied" | "unsupported">("idle");
  const [elapsed, setElapsed] = useState(0);
  const [levels, setLevels] = useState<number[]>(() => Array(BARS).fill(0.08));
  const url = useObjectUrl(audio);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const cleanupRef = useRef<() => void>(() => undefined);

  useEffect(() => () => cleanupRef.current(), []);

  const stop = useCallback(() => {
    if (recorderRef.current?.state === "recording") recorderRef.current.stop();
  }, []);

  async function start() {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === "undefined") {
      setState("unsupported");
      return;
    }
    let stream: MediaStream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } });
    } catch {
      setState("denied");
      return;
    }

    const mimeType = pickMimeType();
    const recorder = new MediaRecorder(stream, mimeType ? { mimeType, audioBitsPerSecond: 32000 } : undefined);
    const chunks: Blob[] = [];
    const startedAt = performance.now();

    const AudioCtx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    const context = AudioCtx ? new AudioCtx() : null;
    const analyser = context?.createAnalyser();
    if (context && analyser) {
      analyser.fftSize = 256;
      context.createMediaStreamSource(stream).connect(analyser);
    }
    const data = new Uint8Array(analyser?.frequencyBinCount ?? 0);
    let frame = 0;
    let lastPush = 0;
    const tick = (now: number) => {
      const ms = now - startedAt;
      setElapsed(ms);
      if (analyser && now - lastPush > 90) {
        lastPush = now;
        analyser.getByteTimeDomainData(data);
        let peak = 0;
        for (const v of data) peak = Math.max(peak, Math.abs(v - 128) / 128);
        setLevels((prev) => [...prev.slice(1), Math.max(0.08, Math.min(1, peak * 1.8))]);
      }
      if (ms >= MAX_MS) recorder.stop();
      else frame = requestAnimationFrame(tick);
    };

    cleanupRef.current = () => {
      cancelAnimationFrame(frame);
      stream.getTracks().forEach((t) => t.stop());
      void context?.close().catch(() => undefined);
    };

    recorder.ondataavailable = (e) => e.data.size && chunks.push(e.data);
    recorder.onstop = () => {
      const duration = performance.now() - startedAt;
      cleanupRef.current();
      setState("idle");
      setLevels(Array(BARS).fill(0.08));
      const blob = new Blob(chunks, { type: recorder.mimeType || mimeType || "audio/webm" });
      if (blob.size > 0) onAudio(blob, Math.round(duration));
    };

    recorderRef.current = recorder;
    recorder.start(1000);
    setElapsed(0);
    setState("recording");
    frame = requestAnimationFrame(tick);
  }

  const recording = state === "recording";

  return (
    <div className="flex h-full flex-col items-center justify-center gap-6 px-4 py-6 text-center text-cream">
      {audio && url && !recording ? (
        <>
          <div className="w-full max-w-sm rounded-[24px] bg-cream/8 p-4 ring-1 ring-cream/10">
            <p className="mb-3 text-sm font-semibold">Voice note · {formatDuration(durationMs)}</p>
            <audio src={url} controls className="w-full" />
          </div>
          <div className="flex gap-2">
            <button type="button" onClick={() => onAudio(null, null)} className="inline-flex h-12 items-center gap-2 rounded-full bg-cream/10 px-5 text-sm font-semibold hover:bg-cream/15">
              <Trash2 className="size-4" aria-hidden /> Remove
            </button>
            <button type="button" onClick={() => void start()} className="inline-flex h-12 items-center gap-2 rounded-full bg-cream px-5 text-sm font-semibold text-ink hover:bg-white">
              <RotateCcw className="size-4" aria-hidden /> Record again
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="flex h-24 w-full max-w-sm items-center justify-center gap-[3px]" aria-hidden>
            {levels.map((level, i) => (
              <span key={i} className={cn("w-[5px] rounded-full transition-[height] duration-100", recording ? "bg-terracotta" : "bg-cream/25")} style={{ height: `${Math.round(level * 96)}px` }} />
            ))}
          </div>
          <p className="text-[40px] font-semibold leading-none tracking-[-0.03em] tabular" aria-live="off">
            {formatDuration(elapsed)}
          </p>
          <p className="max-w-xs text-sm leading-relaxed text-cream/70">
            {state === "denied"
              ? "Microphone access is blocked. Allow it in your browser settings, or type your report instead."
              : state === "unsupported"
                ? "This browser can't record audio. Type your report instead."
                : recording
                  ? "Recording… say what you see, how many animals, and since when."
                  : "Tap to record. Speak naturally — English, Pidgin, Yoruba, Hausa or Igbo. Up to 3 minutes."}
          </p>
          <div className="relative">
            {recording && <span className="absolute inset-0 animate-pulse-ring rounded-full bg-terracotta" aria-hidden />}
            <button
              type="button"
              onClick={() => (recording ? stop() : void start())}
              disabled={state === "unsupported"}
              aria-label={recording ? "Stop recording" : "Start recording"}
              aria-pressed={recording}
              className={cn(
                "relative flex size-20 items-center justify-center rounded-full text-white shadow-lift transition-transform active:scale-95 disabled:opacity-40",
                recording ? "bg-terracotta-ink" : "bg-terracotta",
              )}
            >
              {recording ? <Square className="size-7 fill-current" aria-hidden /> : <Mic className="size-8" aria-hidden />}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
