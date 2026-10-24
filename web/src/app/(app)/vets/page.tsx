import type { Metadata } from "next";
import { VetDirectoryScreen } from "@/components/vets/vet-screens";
export const metadata: Metadata = { title: "Veterinarians" };
export default function Page() {
  return <VetDirectoryScreen />;
}
