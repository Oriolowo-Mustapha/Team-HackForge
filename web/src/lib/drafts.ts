"use client";

import { clear, createStore, del, entries, get, set } from "idb-keyval";

/**
 * Unsent health-report drafts kept on this device (IndexedDB) so a failed upload, an AI outage or a
 * lost connection never loses the farmer's words, photo or voice note. Drafts keep their idempotency
 * key, so retries reuse the same report. Sign-out attempts to remove local data; owner checks also
 * keep drafts hidden from other accounts if browser storage blocks cleanup.
 */

export type ReportDraft = {
  id: string;
  ownerUserId: string;
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
  if (!draft.ownerUserId) throw new Error("Sign in before saving a draft.");
  await set(draft.id, { ...draft, updatedAt: Date.now() }, db());
}

export async function getDraft(id: string, ownerUserId: string): Promise<ReportDraft | undefined> {
  if (!ownerUserId) return undefined;
  const draft = await get<ReportDraft>(id, db());
  // Legacy drafts have no authenticated owner and must remain hidden.
  return draft?.ownerUserId === ownerUserId ? draft : undefined;
}

export async function listDrafts(ownerUserId: string): Promise<ReportDraft[]> {
  if (!ownerUserId) return [];
  const all = await entries<string, ReportDraft>(db());
  return all.map(([, value]) => value).filter((draft) => draft.ownerUserId === ownerUserId).sort((a, b) => b.updatedAt - a.updatedAt);
}

export async function deleteDraft(id: string): Promise<void> {
  await del(id, db());
}

/** Removes everything this app stored on the device (used on sign-out and session expiry). */
export async function clearLocalData(): Promise<boolean> {
  let cleared = true;
  try {
    await clear(db());
  } catch {
    // A transient block can leave old rows on disk. Owner filtering still prevents account crossover.
    cleared = false;
  }
  try {
    for (const key of Object.keys(localStorage)) if (key.startsWith("fh:")) localStorage.removeItem(key);
  } catch {
    cleared = false;
  }
  try {
    if ("caches" in window) {
      for (const name of await caches.keys()) if (name.startsWith("fh-runtime")) await caches.delete(name);
    }
  } catch {
    cleared = false;
  }
  return cleared;
}
