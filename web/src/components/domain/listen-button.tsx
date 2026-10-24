"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Volume2, VolumeX, Loader2 } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import { voiceApi } from "@/lib/api/endpoints";
import { cn } from "@/lib/cn";

type ListenState = "idle" | "loading" | "playing";

/**
 * A "Listen" button that plays a text as speech (Spitch, with YarnGPT fallback) or,
 * when `src` is given, plays that ready-made audio — optionally auto-playing, which
 * FarmHand uses to voice the assessment reply when the report arrived as a voice note.
 */
export function ListenButton({
  text,
  label = "Listen",
  voice,
  targetLanguage,
  variant = "inline",
  className,
  src,
  autoPlay = false,
  playLabel,
}: {
  /** The text to synthesize. If empty/null (and no src), the button is hidden. */
  text: string | null | undefined;
  /** Button label shown next to the icon. */
  label?: string;
  /** Spitch voice name override (e.g. "sade", "umar", "chiamaka", "amina"). */
  voice?: string;
  /** ISO 639-1 language hint (e.g. "yo", "ha", "ig", "pcm", "en"). */
  targetLanguage?: string;
  /** "inline" for a small text button, "pill" for a standalone accent pill. */
  variant?: "inline" | "pill";
  className?: string;
  /** Ready-made audio URL to play instead of synthesizing from text. */
  src?: string | null;
  /** Start playback on mount (used for auto-playing the voice reply after confirm). */
  autoPlay?: boolean;
  /** Label to use for a ready-made audio (defaults to `label`). */
  playLabel?: string;
}) {
  const [state, setState] = useState<ListenState>(autoPlay && src ? "loading" : "idle");
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const stop = useCallback(() => {
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current = null;
    }
    setState("idle");
  }, []);

  const playUrl = useCallback((url: string) => {
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current = null;
    }
    const audio = new Audio(url);
    audioRef.current = audio;

    audio.addEventListener("playing", () => setState("playing"));
    audio.addEventListener("ended", () => setState("idle"));
    audio.addEventListener("error", () => setState("idle"));
    audio.addEventListener("pause", () => {
      if (audioRef.current === audio) setState("idle");
    });

    audio.play().catch(() => setState("idle"));
  }, []);

  const speak = useMutation({
    mutationFn: () => voiceApi.speak(text!.slice(0, 1500), { voice, targetLanguage }),
    onSuccess: (data) => playUrl(data.audioUrl),
    onError: () => setState("idle"),
  });

  // Auto-play a ready-made response (the farmer just confirmed by clicking, so playback is allowed).
  useEffect(() => {
    if (!autoPlay || !src) return;
    const audio = new Audio(src);
    audioRef.current = audio;

    const onPlaying = () => setState("playing");
    const onIdle = () => setState("idle");
    audio.addEventListener("playing", onPlaying);
    audio.addEventListener("ended", onIdle);
    audio.addEventListener("error", onIdle);
    audio.addEventListener("pause", onIdle);
    audio.play().catch(() => setState("idle"));

    return () => {
      audio.pause();
      audio.removeEventListener("playing", onPlaying);
      audio.removeEventListener("ended", onIdle);
      audio.removeEventListener("error", onIdle);
      audio.removeEventListener("pause", onIdle);
      if (audioRef.current === audio) audioRef.current = null;
    };
  }, [autoPlay, src]);

  // Release the audio when the screen goes away.
  useEffect(() => () => stop(), [stop]);

  const toggle = useCallback(() => {
    if (state === "playing") {
      stop();
      return;
    }
    if (src) {
      playUrl(src);
      return;
    }
    setState("loading");
    speak.mutate();
  }, [state, src, stop, speak, playUrl]);

  if (!text?.trim() && !src) return null;

  const Icon = state === "loading" ? Loader2 : state === "playing" ? VolumeX : Volume2;
  const actionLabel = state === "playing" ? "Stop" : state === "loading" ? "Loading…" : src ? playLabel ?? label : label;

  if (variant === "pill") {
    return (
      <button
        type="button"
        onClick={toggle}
        disabled={state === "loading"}
        aria-label={state === "playing" ? "Stop audio" : actionLabel}
        className={cn(
          "inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition-all duration-200",
          state === "playing"
            ? "bg-terracotta-ink text-white shadow-soft hover:bg-[#953f23]"
            : "bg-sand text-ink hover:bg-[#ece6da] active:bg-[#e4ddcf]",
          state === "loading" && "animate-pulse",
          className,
        )}
      >
        <Icon className={cn("size-4 shrink-0", state === "loading" && "animate-spin")} aria-hidden />
        {actionLabel}
      </button>
    );
  }

  // Inline variant — a minimal text+icon button
  return (
    <button
      type="button"
      onClick={toggle}
      disabled={state === "loading"}
      aria-label={state === "playing" ? "Stop audio" : actionLabel}
      className={cn(
        "inline-flex items-center gap-1.5 text-sm font-semibold transition-colors duration-150",
        state === "playing"
          ? "text-terracotta-ink"
          : "text-muted hover:text-ink",
        state === "loading" && "animate-pulse",
        className,
      )}
    >
      <Icon className={cn("size-4 shrink-0", state === "loading" && "animate-spin")} aria-hidden />
      {actionLabel}
    </button>
  );
}
