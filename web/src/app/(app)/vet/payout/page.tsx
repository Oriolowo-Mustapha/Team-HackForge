import type { Metadata } from "next";
import { PayoutScreen } from "@/components/vets/vet-screens";
export const metadata: Metadata = { title: "Payout account" };
export default function Page() {
  return <PayoutScreen />;
}
