"use client";

import { Camera, CloudOff, Mic, Trash2, Type } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { deleteDraft, listDrafts, type ReportDraft } from "@/lib/drafts";
import { formatRelative } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/states";
import { formatDuration } from "./voice-recorder";

/** Reports saved on this device that haven't been sent yet. */
export function DraftsList({ farmId }: { farmId: string }) {
  const [drafts, setDrafts] = useState<ReportDraft[] | null>(null);

  useEffect(() => {
    listDrafts()
      .then((all) => setDrafts(all.filter((d) => d.farmId === farmId)))
      .catch(() => setDrafts([]));
  }, [farmId]);

  if (drafts === null) return null;
  if (drafts.length === 0)
    return (
      <EmptyState icon={<CloudOff />} title="No unsent reports">
        If you lose signal while sending a report, it&apos;s kept here until you&apos;re back online.
      </EmptyState>
    );

  return (
    <ul className="space-y-2.5">
      {drafts.map((d) => (
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
                await deleteDraft(d.id);
                setDrafts((list) => list?.filter((x) => x.id !== d.id) ?? []);
              }}
            >
              Delete
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}
