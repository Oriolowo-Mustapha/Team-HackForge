"use client";

import { Check, CircleDashed, UploadCloud } from "lucide-react";
import { motion } from "motion/react";
import { cn } from "@/lib/cn";
import { Spinner } from "@/components/ui/spinner";

export type Stage = { key: string; label: string; detail?: string };

/**
 * Honest progress: the upload percentage is real (measured by the browser). The server-side steps
 * run as one request, so they are shown as "in progress" together and ticked only when the response
 * arrives — no invented timings or percentages.
 */
export function AnalysisProgress({ upload, done, stages, onCancel }: { upload: number; done: boolean; stages: Stage[]; onCancel?: () => void }) {
  const uploading = upload < 1 && !done;
  return (
    <div className="flex flex-col items-center px-6 py-10 text-center text-cream" role="status" aria-live="polite">
      <div className="relative flex size-28 items-center justify-center">
        <svg viewBox="0 0 120 120" className="absolute inset-0 -rotate-90" aria-hidden>
          <circle cx="60" cy="60" r="54" fill="none" stroke="currentColor" strokeOpacity="0.12" strokeWidth="6" />
          <motion.circle
            cx="60"
            cy="60"
            r="54"
            fill="none"
            stroke="#C15935"
            strokeWidth="6"
            strokeLinecap="round"
            strokeDasharray={339.3}
            animate={{ strokeDashoffset: 339.3 * (1 - (done ? 1 : uploading ? upload : 1)) }}
            transition={{ duration: 0.3 }}
          />
        </svg>
        {done ? <Check className="size-10" aria-hidden /> : uploading ? <span className="text-2xl font-semibold tabular">{Math.round(upload * 100)}%</span> : <Spinner className="size-9 text-cream/80" />}
      </div>
      <h2 className="mt-6 text-[22px] font-semibold tracking-[-0.02em]">{done ? "Ready for your review" : uploading ? "Sending your report" : "Analysing your report"}</h2>
      <p className="mt-1 max-w-xs text-sm text-cream/65">{done ? "Opening the summary…" : uploading ? "Keep this screen open on a slow connection." : "This usually takes a few seconds."}</p>

      <ol className="mt-8 w-full max-w-sm space-y-2 text-left">
        <li className="flex items-center gap-3 rounded-2xl bg-cream/6 px-4 py-3">
          <span className={cn("flex size-7 items-center justify-center rounded-full", upload >= 1 || done ? "bg-green text-white" : "bg-cream/10")}>
            {upload >= 1 || done ? <Check className="size-4" aria-hidden /> : <UploadCloud className="size-4" aria-hidden />}
          </span>
          <span className="flex-1 text-sm font-semibold">Upload</span>
          <span className="text-xs text-cream/60 tabular">{upload >= 1 || done ? "Done" : `${Math.round(upload * 100)}%`}</span>
        </li>
        {stages.map((stage) => (
          <li key={stage.key} className="flex items-center gap-3 rounded-2xl bg-cream/6 px-4 py-3">
            <span className={cn("flex size-7 items-center justify-center rounded-full", done ? "bg-green text-white" : "bg-cream/10")}>
              {done ? <Check className="size-4" aria-hidden /> : uploading ? <CircleDashed className="size-4 text-cream/50" aria-hidden /> : <Spinner className="size-4" />}
            </span>
            <span className="min-w-0 flex-1">
              <span className="block text-sm font-semibold">{stage.label}</span>
              {stage.detail && <span className="block text-xs text-cream/55">{stage.detail}</span>}
            </span>
          </li>
        ))}
      </ol>
      {onCancel && !done && (
        <button type="button" onClick={onCancel} className="mt-6 rounded-xl px-4 py-2 text-sm font-semibold text-cream/70 hover:text-cream">
          Stop and keep as draft
        </button>
      )}
    </div>
  );
}
