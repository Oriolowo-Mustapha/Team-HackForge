"use client";

import { Camera, ImageUp, RefreshCw, SwitchCamera, VideoOff } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { useObjectUrl } from "@/lib/hooks/use-object-url";
import { ACCEPTED_IMAGE_TYPES, compressImage } from "@/lib/image-tools";
import { Spinner } from "@/components/ui/spinner";

type CameraState = "idle" | "starting" | "live" | "denied" | "unavailable";

/**
 * Viewfinder for the health scanner. Uses the rear camera when the browser allows it and always
 * offers a file picker (which opens the native camera on phones) as a fallback.
 */
export function CameraCapture({ image, onImage }: { image: Blob | null; onImage: (blob: Blob | null) => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const [state, setState] = useState<CameraState>("idle");
  const [facing, setFacing] = useState<"environment" | "user">("environment");
  const [busy, setBusy] = useState(false);
  const preview = useObjectUrl(image);

  const stop = useCallback(() => {
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
  }, []);

  useEffect(() => stop, [stop]);

  const start = useCallback(
    async (mode: "environment" | "user" = facing) => {
      if (!navigator.mediaDevices?.getUserMedia) {
        setState("unavailable");
        return;
      }
      setState("starting");
      stop();
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: mode }, width: { ideal: 1920 }, height: { ideal: 1440 } },
          audio: false,
        });
        streamRef.current = stream;
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          await videoRef.current.play().catch(() => undefined);
        }
        setState("live");
      } catch (error) {
        const name = error instanceof DOMException ? error.name : "";
        setState(name === "NotAllowedError" || name === "SecurityError" ? "denied" : "unavailable");
      }
    },
    [facing, stop],
  );

  async function capture() {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;
    setBusy(true);
    const scale = Math.min(1, 1600 / Math.max(video.videoWidth, video.videoHeight));
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(video.videoWidth * scale);
    canvas.height = Math.round(video.videoHeight * scale);
    canvas.getContext("2d")?.drawImage(video, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", 0.85));
    setBusy(false);
    if (blob) {
      stop();
      setState("idle");
      onImage(blob);
    }
  }

  async function pickFile(file: File | undefined) {
    if (!file) return;
    setBusy(true);
    stop();
    setState("idle");
    onImage(await compressImage(file));
    setBusy(false);
  }

  return (
    <div className="relative flex h-full flex-col">
      <div className="relative min-h-0 flex-1 overflow-hidden rounded-[26px] bg-ink-2">
        {preview ? (
          // eslint-disable-next-line @next/next/no-img-element -- local object URL preview
          <img src={preview} alt="Photo attached to this report" className="absolute inset-0 size-full object-cover" />
        ) : (
          <>
            <video
              ref={videoRef}
              playsInline
              muted
              aria-label="Camera viewfinder"
              className={cn("absolute inset-0 size-full object-cover transition-opacity", state === "live" ? "opacity-100" : "opacity-0")}
            />
            {state !== "live" && (
              <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 px-8 text-center text-cream">
                {state === "starting" ? (
                  <Spinner className="size-7 text-cream/70" label="Starting camera" />
                ) : state === "denied" || state === "unavailable" ? (
                  <>
                    <VideoOff className="size-8 text-cream/60" aria-hidden />
                    <p className="text-sm text-cream/80">
                      {state === "denied" ? "Camera access is blocked. Allow it in your browser settings, or choose a photo instead." : "No camera is available here. Choose a photo instead."}
                    </p>
                  </>
                ) : (
                  <>
                    <span className="flex size-16 items-center justify-center rounded-full bg-cream/8 ring-1 ring-cream/15">
                      <Camera className="size-7 text-cream" aria-hidden />
                    </span>
                    <p className="max-w-[16rem] text-sm leading-relaxed text-cream/75">Get close to the problem — eyes, droppings, a swelling, a wound — in good light.</p>
                  </>
                )}
              </div>
            )}
          </>
        )}

        {/* Viewfinder frame */}
        {!preview && (
          <div className="pointer-events-none absolute inset-6" aria-hidden>
            {["left-0 top-0 border-l-[3px] border-t-[3px] rounded-tl-[18px]", "right-0 top-0 border-r-[3px] border-t-[3px] rounded-tr-[18px]", "bottom-0 left-0 border-b-[3px] border-l-[3px] rounded-bl-[18px]", "bottom-0 right-0 border-b-[3px] border-r-[3px] rounded-br-[18px]"].map((c) => (
              <span key={c} className={cn("absolute size-10 border-cream/85", c)} />
            ))}
            {state === "live" && <span className="absolute inset-x-3 top-0 h-[2px] animate-scan-line rounded-full bg-terracotta shadow-[0_0_16px_2px_rgb(193_89_53/0.7)]" />}
          </div>
        )}
        {preview && <span className="absolute left-4 top-4 rounded-full bg-ink/70 px-3 py-1 text-xs font-semibold text-cream backdrop-blur">Photo attached</span>}
      </div>

      <div className="mt-4 flex items-center justify-between gap-3 px-2">
        <button
          type="button"
          onClick={() => fileRef.current?.click()}
          className="inline-flex size-12 items-center justify-center rounded-2xl bg-cream/10 text-cream hover:bg-cream/15"
          aria-label="Choose a photo from your device"
          title="Choose a photo"
        >
          <ImageUp className="size-5" aria-hidden />
        </button>
        <input ref={fileRef} type="file" accept={ACCEPTED_IMAGE_TYPES} capture="environment" className="sr-only" tabIndex={-1} aria-hidden onChange={(e) => void pickFile(e.target.files?.[0]).finally(() => (e.target.value = ""))} />

        {preview ? (
          <button
            type="button"
            onClick={() => {
              onImage(null);
              void start();
            }}
            className="inline-flex h-14 items-center gap-2 rounded-full bg-cream px-6 text-[15px] font-semibold text-ink hover:bg-white"
          >
            <RefreshCw className="size-4" aria-hidden /> Retake
          </button>
        ) : state === "live" ? (
          <button
            type="button"
            onClick={() => void capture()}
            disabled={busy}
            aria-label="Take photo"
            className="relative flex size-[72px] items-center justify-center rounded-full bg-cream ring-4 ring-cream/25 transition-transform active:scale-95 disabled:opacity-60"
          >
            <span className="size-[58px] rounded-full border-2 border-ink/80 bg-terracotta" />
          </button>
        ) : (
          <button type="button" onClick={() => void start()} disabled={state === "starting" || busy} className="inline-flex h-14 items-center gap-2 rounded-full bg-terracotta px-6 text-[15px] font-semibold text-white hover:bg-terracotta-ink disabled:opacity-60">
            {busy ? <Spinner className="size-4" /> : <Camera className="size-5" aria-hidden />} Open camera
          </button>
        )}

        <button
          type="button"
          onClick={() => {
            const next = facing === "environment" ? "user" : "environment";
            setFacing(next);
            void start(next);
          }}
          disabled={state !== "live"}
          className="inline-flex size-12 items-center justify-center rounded-2xl bg-cream/10 text-cream hover:bg-cream/15 disabled:opacity-30"
          aria-label="Switch camera"
          title="Switch camera"
        >
          <SwitchCamera className="size-5" aria-hidden />
        </button>
      </div>
    </div>
  );
}
