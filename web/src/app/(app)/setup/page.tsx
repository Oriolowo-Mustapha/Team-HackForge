import type { Metadata } from "next";
import { SetupFlow } from "./setup-flow";

export const metadata: Metadata = { title: "Set up your farm" };

export default async function SetupPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  return <SetupFlow another={params.another === "1"} />;
}
