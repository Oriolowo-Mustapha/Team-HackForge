import type { Metadata } from "next";
import { AdminAuditScreen } from "@/components/admin/admin-screens";
export const metadata: Metadata = { title: "Audit log" };
export default function Page() {
  return <AdminAuditScreen />;
}
