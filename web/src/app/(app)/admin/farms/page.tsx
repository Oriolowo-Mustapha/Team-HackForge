import type { Metadata } from "next";
import { AdminFarmsScreen } from "@/components/admin/admin-screens";
export const metadata: Metadata = { title: "Farms" };
export default function Page() {
  return <AdminFarmsScreen />;
}
