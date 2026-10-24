import type { Metadata } from "next";
import { AdminOverviewScreen } from "@/components/admin/admin-screens";
export const metadata: Metadata = { title: "Platform overview" };
export default function Page() {
  return <AdminOverviewScreen />;
}
