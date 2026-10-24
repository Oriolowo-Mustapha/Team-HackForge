import type { Metadata } from "next";
import { AdminVetsScreen } from "@/components/admin/admin-screens";
export const metadata: Metadata = { title: "Veterinarians" };
export default function Page() {
  return <AdminVetsScreen />;
}
