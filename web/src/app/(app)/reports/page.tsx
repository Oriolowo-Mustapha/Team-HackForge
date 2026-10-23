import type { Metadata } from "next";
import { ReportsScreen } from "./reports-screen";

export const metadata: Metadata = { title: "Health reports" };

export default async function ReportsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  return <ReportsScreen initialStatus={params.status === "pending" ? "pending" : "all"} />;
}
