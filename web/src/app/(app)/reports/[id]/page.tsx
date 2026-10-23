import type { Metadata } from "next";
import { ReportDetail } from "./report-detail";

export const metadata: Metadata = { title: "Health report" };

export default async function ReportPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  return <ReportDetail id={id} isNew={query.new === "1"} />;
}
