"use client";

import { Camera, CloudOff, Mic, Trash2, Type } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { deleteDraft, listDrafts, type ReportDraft } from "@/lib/drafts";
import { formatRelative } from "@/lib/format";
import { useSession } from "@/lib/hooks/use-session";
import { Button } from "@/components/ui/button";
import { EmptyState, ErrorState, InlineError } from "@/components/ui/states";
import { formatDuration } from "./voice-recorder";

/** Reports saved on this device that haven't been sent yet. */
export function DraftsList({ farmId }: { farmId: string }) {
  const { user } = useSession();
  const ownerUserId = user?.userId ?? "";
  const [drafts, setDrafts] = useState<ReportDraft[] | null>(null);
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    let cancelled = false;
    listDrafts(ownerUserId)
      .then((all) => { if (!cancelled) setDrafts(all.filter((d) => d.farmId === farmId)); })
      .catch(() => { if (!cancelled) setError(new Error("Drafts could not be read from this device. Allow browser storage, then reopen this list.")); });
    return () => { cancelled = true; };
  }, [farmId, ownerUserId]);

  const visibleDrafts = drafts?.filter((draft) => draft.ownerUserId === ownerUserId && draft.farmId === farmId);
  if (error && visibleDrafts === undefined) return <ErrorState error={error} />;
  if (visibleDrafts === undefined) return <p role="status" className="text-sm text-muted">Loading drafts…</p>;
  if (visibleDrafts.length === 0)
    return (
      <EmptyState icon={<CloudOff />} title="No unsent reports">
        Successfully saved drafts appear here so you can send them when you&apos;re back online.
      </EmptyState>
    );

  return (
    <><InlineError error={error} /><ul className="space-y-2.5">
      {visibleDrafts.map((d) => (
        <li key={d.id} className="rounded-[24px] bg-paper p-4 ring-1 ring-line/60">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="font-semibold">{d.subjectName}</p>
              <p className="line-clamp-2 text-sm text-muted">{d.text || (d.audio ? "Voice note" : d.image ? "Photo" : "Empty draft")}</p>
              <p className="mt-1.5 flex flex-wrap items-center gap-2 text-xs text-faint">
                Saved {formatRelative(new Date(d.updatedAt))}
                {d.image && <Camera className="size-3.5" aria-label="Photo" />}
                {d.audio && (
                  <span className="inline-flex items-center gap-1">
                    <Mic className="size-3.5" aria-hidden /> {formatDuration(d.audioDurationMs)}
                  </span>
                )}
                {d.text && <Type className="size-3.5" aria-label="Text" />}
              </p>
              {d.lastError && <p className="mt-1.5 text-xs font-medium text-terracotta-deep">Last attempt: {d.lastError}</p>}
            </div>
          </div>
          <div className="mt-3 flex gap-2">
            <Link href={`/scan?draft=${d.id}`} className="inline-flex h-10 flex-1 items-center justify-center rounded-[14px] bg-ink text-sm font-semibold text-cream hover:bg-ink-2">
              Open and send
            </Link>
            <Button
              size="sm"
              variant="secondary"
              aria-label="Delete draft"
              icon={<Trash2 className="size-4" />}
              onClick={async () => {
                try {
                  await deleteDraft(d.id);
                  setDrafts((list) => list?.filter((x) => x.id !== d.id) ?? []);
                  setError(null);
                } catch {
                  setError(new Error("This draft could not be deleted. Please try again."));
                }
              }}
            >
              Delete
            </Button>
          </div>
        </li>
      ))}
    </ul></>
  );
}
