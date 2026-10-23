import type { Metadata } from "next";
import { CaseFile } from "@/components/case/case-file";

export const metadata: Metadata = { title: "Case" };

export default async function CasePage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  return <CaseFile id={id} openRequest={query.request === "1"} />;
}
