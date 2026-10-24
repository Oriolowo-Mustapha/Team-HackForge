import type { Metadata } from "next";
import { AdminSystemScreen } from "@/components/admin/admin-screens";
export const metadata: Metadata = { title: "System health" };
export default function Page() {
  return <AdminSystemScreen />;
}
