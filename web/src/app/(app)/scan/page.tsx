import type { Metadata } from "next";
import { Scanner } from "@/components/scan/scanner";

export const metadata: Metadata = { title: "Report a concern" };

export default async function ScanPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const one = (v: string | string[] | undefined) => (typeof v === "string" ? v : undefined);
  const group = one(params.group);
  const animal = one(params.animal);
  const draft = one(params.draft);
  return <Scanner key={draft ?? animal ?? group ?? "new"} initial={{ group, animal }} draftId={draft} showDrafts={params.drafts === "1"} />;
}
