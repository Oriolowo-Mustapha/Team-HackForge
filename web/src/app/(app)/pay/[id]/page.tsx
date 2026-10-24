import type { Metadata } from "next";
import { PayScreen } from "./pay-screen";

export const metadata: Metadata = { title: "Review and pay" };

export default async function PayPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <PayScreen vetRequestId={id} />;
}
