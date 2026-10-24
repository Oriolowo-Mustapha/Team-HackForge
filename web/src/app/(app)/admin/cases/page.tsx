import type { Metadata } from "next";
import { AdminCasesScreen } from "@/components/admin/admin-screens";
export const metadata: Metadata = { title: "Cases" };
export default function Page() {
  return <AdminCasesScreen />;
}
