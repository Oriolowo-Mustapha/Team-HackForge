"use client";

import { clear, createStore, del, entries, get, set } from "idb-keyval";

/**
 * Unsent health-report drafts kept on this device (IndexedDB) so a failed upload, an AI outage or a
 * lost connection never loses the farmer's words, photo or voice note. Drafts keep their idempotency
 * key, so retrying can never create a duplicate report. Everything is wiped on sign-out.
 */

export type ReportDraft = {
  id: string;
  idempotencyKey: string;
  farmId: string;
  subjectKind: "animal" | "group";
  subjectId: string;
  subjectName: string;
  text: string;
  image: Blob | null;
  audio: Blob | null;
  audioDurationMs: number | null;
  manual: { mortalityCount: number | null; feed: string; water: string; eggs: string } | null;
  lastError: string | null;
  createdAt: number;
  updatedAt: number;
};

let store: ReturnType<typeof createStore> | null = null;
const db = () => (store ??= createStore("farmhand", "report-drafts"));

export async function saveDraft(draft: ReportDraft): Promise<void> {
  await set(draft.id, { ...draft, updatedAt: Date.now() }, db());
}

export async function getDraft(id: string): Promise<ReportDraft | undefined> {
  return get<ReportDraft>(id, db());
}

export async function listDrafts(): Promise<ReportDraft[]> {
  const all = await entries<string, ReportDraft>(db());
  return all.map(([, value]) => value).sort((a, b) => b.updatedAt - a.updatedAt);
}

export async function deleteDraft(id: string): Promise<void> {
  await del(id, db());
}

/** Removes everything this app stored on the device (used on sign-out and session expiry). */
export async function clearLocalData(): Promise<void> {
  try {
    await clear(db());
  } catch {
    // IndexedDB may be unavailable (private mode); nothing to clear.
  }
  try {
    for (const key of Object.keys(localStorage)) if (key.startsWith("fh:")) localStorage.removeItem(key);
  } catch {
    // localStorage may be blocked.
  }
  try {
    if ("caches" in window) {
      for (const name of await caches.keys()) if (name.startsWith("fh-runtime")) await caches.delete(name);
    }
  } catch {
    // Cache Storage may be unavailable.
  }
}
