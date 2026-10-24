import type { Metadata } from "next";
import { VetDashboardScreen } from "@/components/vets/vet-screens";
export const metadata: Metadata = { title: "Veterinary dashboard" };
export default function Page() {
  return <VetDashboardScreen />;
}
